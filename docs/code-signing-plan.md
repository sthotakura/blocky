# Code Signing Plan — Blocky

Goal: Authenticode-sign `Blocky.exe`, `Blocky.Host.exe`, `install.ps1`, and `uninstall.ps1` in the
release pipeline, using Azure Trusted Signing.

## Why Azure Trusted Signing (vs. a traditional OV/EV cert)

Since June 2023, CA/Browser Forum rules require OV/EV code-signing certs to live on a hardware
token or cloud HSM — a plain `.pfx` is no longer issued. A physical USB token can't be used from a
GitHub-hosted runner at all (would need a self-hosted runner with the dongle attached). Azure
Trusted Signing is Microsoft's PKI-as-a-service alternative:

- ~$9.99/mo (Basic SKU), unlimited signing operations.
- No hardware token. CI authenticates via OIDC federated credentials — no secrets stored in GitHub.
- Works natively from `windows-latest` GitHub-hosted runners via `azure/trusted-signing-action`.
- Cert is OV-equivalent: no "unknown publisher" SmartScreen prompt is removed instantly — reputation
  builds gradually with install volume (same as any OV cert; only EV gives instant trust, and EV
  isn't available through this service).

This is the practical choice for a solo/open-source project releasing from GitHub Actions.

## Prerequisites

- Azure CLI installed and logged in (`az login`) — already done, subscription confirmed:
  `India - PAYG - Personal` (`1c371bc1-e704-48eb-9b80-6ba1813fc6be`).
- `trustedsigning` CLI extension — already added (`az extension add --name trustedsigning`).
- A government ID (individual validation) or business registration documents (org validation) for
  the identity-verification step — this part is manual and must be done by you in the Azure Portal.
- Admin rights on the `blocky` GitHub repo (to add the federated credential's audience/subject and
  wire the workflow).

## Phase 1 — Azure CLI: provider + resource group + signing account

These are billable but cheap (~$10/mo starts once the account exists). Pick a resource group name,
region, and account name before running.

```bash
# Register the resource provider (one-time per subscription)
az provider register --namespace Microsoft.CodeSigning
az provider show --namespace Microsoft.CodeSigning --query registrationState -o tsv
# ^ wait until this says "Registered" before continuing

# Resource group
az group create -n rg-blocky-signing -l eastus

# Trusted Signing account (Basic SKU)
az trustedsigning create \
  -n blockysigning \
  -g rg-blocky-signing \
  -l eastus \
  --sku Basic
```

Supported regions (pick one close to you): East US, East US 2 (via Central US EUAP), West US,
West US 2, West US 3, Central US, South Central US, North Central US, West Central US, North
Europe, West Europe, Poland Central, Switzerland North, Brazil South, Japan East, Korea Central,
Mexico Central.

## Phase 2 — Portal: identity validation (manual, you only)

Identity validation is **not** exposed via CLI/ARM — it's a document-upload/KYC flow reviewed by
Microsoft's verification partner, typically 1–3 business days.

1. Go to the Trusted Signing account in the Azure Portal (`rg-blocky-signing` → `blockysigning`).
2. Open **Identity validation** → **Add identity validation**.
3. Choose **Individual** (simpler, ties the cert subject to your personal identity) or
   **Organization** (needs business registration docs) — individual is the right call unless
   Blocky is published under a registered business.
4. Upload the requested ID document(s) and submit.
5. Wait for approval. Once approved, note the **identity validation ID** (GUID) shown on that
   resource — needed for Phase 3.

## Phase 3 — CLI: certificate profile (after Phase 2 is approved)

```bash
az trustedsigning certificate-profile create \
  -g rg-blocky-signing \
  --account-name blockysigning \
  -n blocky-profile \
  --profile-type PublicTrust \
  --identity-validation-id <GUID-from-phase-2>
```

## Phase 4 — CLI: Entra app registration + federated credential (OIDC for GitHub Actions)

No secrets stored — GitHub's OIDC token is trusted directly by Azure AD, scoped to this repo/branch.

```bash
# App registration
az ad app create --display-name blocky-signing-ci
# note the appId (clientId) from the output

APP_ID=<appId-from-above>
az ad sp create --id $APP_ID

# Federated credential scoped to the release workflow on tag pushes
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "blocky-release-tags",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<your-gh-org-or-user>/blocky:ref:refs/tags/v*",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Grant the app the "Trusted Signing Certificate Profile Signer" role on the account
az role assignment create \
  --assignee $APP_ID \
  --role "Trusted Signing Certificate Profile Signer" \
  --scope /subscriptions/1c371bc1-e704-48eb-9b80-6ba1813fc6be/resourceGroups/rg-blocky-signing/providers/Microsoft.CodeSigning/codeSigningAccounts/blockysigning
```

Record for the workflow: `AZURE_CLIENT_ID` (=`$APP_ID`), `AZURE_TENANT_ID`
(`2385bb62-ce62-4cc0-8496-c62757008371`), `AZURE_SUBSCRIPTION_ID`
(`1c371bc1-e704-48eb-9b80-6ba1813fc6be`). These aren't secret (client ID/tenant/subscription IDs
are not credentials by themselves under OIDC) but still store as repo/environment variables or
secrets for tidiness.

## Phase 5 — Wire signing into `.github/workflows/build.yml`

Insert a signing step in the `release` job, after both `dotnet publish` steps but **before**
`Compress-Archive` — you must sign the raw files, not the zip.

```yaml
      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Sign binaries and scripts
        uses: azure/trusted-signing-action@v0.5.1
        with:
          endpoint: https://eus.codesigning.azure.net   # matches account region
          trusted-signing-account-name: blockysigning
          certificate-profile-name: blocky-profile
          files-folder: ${{ github.workspace }}/publish
          files-folder-filter: exe,ps1
          file-digest: SHA256
          timestamp-rfc3161: http://timestamp.acs.microsoft.com
          timestamp-digest: SHA256
```

Add `permissions: id-token: write` to the `release` job (needed for OIDC).

`install.ps1`/`uninstall.ps1` live in `install/` at source but get copied into the publish output
(per the existing `dotnet publish` config) — confirm they land in `./publish` alongside the exes
before this step runs, or add an explicit copy step.

## Phase 6 — Verify

Locally, after downloading a signed release:

```powershell
signtool verify /pa /v .\Blocky.exe
signtool verify /pa /v .\Blocky.Host.exe
Get-AuthenticodeSignature .\install.ps1
```

All should show a valid chain with the Trusted Signing-issued cert and a timestamp.

## Cost summary

- Azure Trusted Signing Basic SKU: ~$9.99/mo, unlimited signs, starts accruing once the account is
  created in Phase 1 (independent of whether identity validation is finished yet).
- No other recurring cost (OIDC federated credential and app registration are free).

## Cleanup (if abandoning this)

```bash
az group delete -n rg-blocky-signing --yes
az ad app delete --id $APP_ID
```

## Status / next action

- [ ] Phase 1 — not started (waiting on: resource group name, region, account name confirmation)
- [ ] Phase 2 — blocked on Phase 1
- [ ] Phase 3 — blocked on Phase 2 approval
- [ ] Phase 4 — can run independently of Phase 2/3 once Phase 1 is done
- [ ] Phase 5 — code change, can be drafted anytime, needs Phase 4 secrets to actually run in CI
- [ ] Phase 6 — after first signed release
