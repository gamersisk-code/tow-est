# Southern Pride Towing Estimator

This repo contains a standalone HTML estimator plus desktop wrappers (Electron and JavaFX) so you can ship a Windows `.exe`.

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

To generate only the portable Windows `.exe` (no installer), run:

```bash
npm run dist:portable
```

The portable `.exe` will be saved under `dist/` with `portable` in the filename.

## Java desktop app (no Electron)

Run the JavaFX desktop app:

```bash
mvn -q javafx:run
```

Build a Windows `.exe` with `jpackage` (requires JDK 17+ and the JavaFX SDK):

```bash
mvn -q -DskipTests package
jpackage --type exe --name "Southern Pride Towing Estimator" \
  --input target --main-jar tow-estimator-1.0.0.jar \
  --main-class com.southernpride.towestimator.EstimatorApp \
  --module-path "%JAVAFX_HOME%\\lib" \
  --add-modules javafx.controls,javafx.web
```

You can also use the helper scripts:

```bash
scripts/build-exe.sh
```

```powershell
scripts/build-exe.ps1
```

## C# standalone Windows `.exe`

The C# Windows app embeds the estimator HTML inside the executable and runs it in WebView2 (no external `.html` file needed at runtime).

Build a portable single-file `.exe` on Windows:

```powershell
dotnet publish TowEstimator.csproj -c Release -r win-x64
```

The portable `.exe` will be under `bin/Release/net8.0-windows/win-x64/publish/`.

You can also run the release scripts:

```powershell
scripts/publish-release.ps1
```

```bash
scripts/publish-release.sh
```

> Note: WebView2 Runtime must be available on the target machine (it ships with Windows 11 and most Windows 10 builds).

## Troubleshooting build errors

If `npm install` or `npm run dist` fails with registry access errors (for example `403 Forbidden`), ensure your environment has access to the public npm registry and any required corporate proxy settings are configured before retrying the install.

If your environment blocks downloads from the default Electron host, try pointing Electron to a mirror before installing:

```bash
npm config set registry https://registry.npmmirror.com
ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/ npm install
```

For PowerShell on Windows:

```powershell
$env:NPM_CONFIG_REGISTRY="https://registry.npmmirror.com"
$env:ELECTRON_MIRROR="https://npmmirror.com/mirrors/electron/"
npm install
```
