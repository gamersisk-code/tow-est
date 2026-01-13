$ErrorActionPreference = "Stop"

if (-not $env:JAVAFX_HOME) {
  Write-Error "JAVAFX_HOME must be set to the JavaFX SDK path (e.g. C:\\javafx-sdk-21)."
}

mvn -q -DskipTests package

jpackage --type exe --name "Southern Pride Towing Estimator" `
  --input target --main-jar tow-estimator-1.0.0.jar `
  --main-class com.southernpride.towestimator.EstimatorApp `
  --module-path "$env:JAVAFX_HOME\\lib" `
  --add-modules javafx.controls,javafx.web
