# Blocky

Blocky is a simple website blocker for Windows.

It lets you define domains and schedules, and blocks access using a PAC (Proxy Auto-Config) file — no admin access or browser extensions required.

---

## ✅ Features

- Block specific websites by domain
- Schedule blocks (e.g., 9am–5pm)
- No browser extension needed
- Runs locally, no admin privileges
- Works with most modern browsers

---

## 🚀 How to Use

1. [Download the latest release](https://github.com/your-username/blocky/releases)
2. Extract `blocky.zip`
3. Run `Blocky.exe`
4. Add block rules and start the service

---

## ⚙️ How It Works

- Hosts a PAC file via local web server
- Routes blocked domains through a local proxy
- Blocks access or shows a local `blocked.html` page

---

## 🧪 Known Limitations

- HTTPS requests may show a “tunnel connection failed” error instead of a custom block page
- Some browsers may cache the PAC file aggressively

---

## 📦 Planned for v1.0

- Browser extension for better block page control
- App auto-launch on startup
- Improved PAC file refresh handling

---

## ❗ Disclaimer

This is an experimental tool — use at your own discretion.
