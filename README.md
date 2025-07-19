# Blocky

**Blocky** is a focused, no-nonsense website blocker for Windows.

It lets you define time-based blocking rules for domains and enforces them through a local WebSocket server and a lightweight Chrome extension — no admin rights, no proxy hacks, no broken SSL.

---

## ✅ Features

- Block specific websites by domain
- Set schedules (e.g., block 9am–5pm)
- Chrome Extension for clean in-browser blocking
- No admin rights required
- No system-wide proxy, no PAC file

---

## 🚀 How to Use

1. [Download the latest release](https://github.com/your-username/blocky/releases)
2. Extract `blocky.zip`
3. Run `Blocky.exe` (no installer needed)
4. Open Chrome and load the extension:
   - Go to `chrome://extensions`
   - Enable **Developer Mode**
   - Click **Load Unpacked**
   - Select the `extension/` folder
5. Add block rules in the app UI — you're done!

---

## ⚙️ How It Works

- `Blocky.exe` runs a local server on port `8080`
- Chrome Extension connects via WebSocket to receive active blocklist
- When a blocked domain is visited, the request is intercepted and the user sees `blocked.html`
- Rules are updated in real-time without needing restarts

---

## 🧪 Known Limitations

- Works only in Chrome (for now)
- Other browsers won’t be blocked unless support is added
- Extension must be installed and running
- WebSocket connection must stay active

---

## 📦 GitHub Releases

Releases include:
- `blocky.zip` → self-contained app
- `blocky-chrome-extension.zip` → Chrome extension

All built and packaged automatically via GitHub Actions.

---

## 🔮 Roadmap

- [ ] Auto-launch on system startup
- [ ] Firefox extension
- [ ] Pomodoro mode / temporary focus blocks
- [ ] Stats and productivity tracking

---

## ❗ Disclaimer

This is an experimental tool. Use responsibly. Feedback and PRs welcome!
