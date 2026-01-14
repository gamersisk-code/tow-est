## Tow Estimator Desktop App

This repository contains a portable Windows-friendly tow estimator written in
Python/Tkinter. The app is designed to be packaged as a single `.exe` so it can
run directly from a USB drive while keeping its logs on that same drive.

### Build a Windows `.exe`

1. Install Python 3.10+ on a Windows machine.
2. Install PyInstaller:

   ```bash
   py -m pip install --upgrade pip
   py -m pip install pyinstaller
   ```

3. Build the executable:

   ```bash
   py -m PyInstaller --onefile --windowed --name TowEstimator tow_estimator.py
   ```

4. The executable will be located at:

   ```
   dist/TowEstimator.exe
   ```

Copy `TowEstimator.exe` onto your USB drive. The log file
`tow_estimator_logs.jsonl` will be created alongside the `.exe` the first time
you save a quote.

### Run the app from USB

Double-click `TowEstimator.exe`. Use the **Estimate** tab to calculate costs,
then click **Save Quote** to store it. Use the **Quotes** tab to search by
customer name, phone, or address.
