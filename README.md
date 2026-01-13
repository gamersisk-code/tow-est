# Southern Pride Towing Estimator

This repo contains a standalone HTML estimator and a minimal Electron wrapper so you can ship a Windows `.exe`.

## Run the estimator in a browser

Open `tow-estimator.html` directly in a browser, or serve it locally for testing:

```bash
python3 -m http.server 8000
```

Then visit <http://localhost:8000/tow-estimator.html>.

## Build a Windows `.exe`

1. Install dependencies:

```bash
npm install
```

2. Launch the desktop app locally:

```bash
npm run start
```

3. Build the installer/exe:

```bash
npm run dist
```

The packaged app will be written to the `dist/` directory. For Windows you will get:

- An installer (`.exe`) built with NSIS.
- A standalone portable `.exe` that can run without installing.

If you are already on Windows, you can run the Windows-only build step:

```bash
npm run dist:win
```
