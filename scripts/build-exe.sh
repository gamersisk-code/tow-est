#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${JAVAFX_HOME:-}" ]]; then
  echo "JAVAFX_HOME must be set to the JavaFX SDK path (e.g. /opt/javafx-sdk-21)." >&2
  exit 1
fi

mvn -q -DskipTests package

jpackage --type exe --name "Southern Pride Towing Estimator" \
  --input target --main-jar tow-estimator-1.0.0.jar \
  --main-class com.southernpride.towestimator.EstimatorApp \
  --module-path "${JAVAFX_HOME}/lib" \
  --add-modules javafx.controls,javafx.web
