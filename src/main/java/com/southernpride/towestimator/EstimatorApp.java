package com.southernpride.towestimator;

import javafx.application.Application;
import javafx.scene.Scene;
import javafx.scene.layout.BorderPane;
import javafx.scene.web.WebView;
import javafx.stage.Stage;

import java.net.URL;

public class EstimatorApp extends Application {
  @Override
  public void start(Stage stage) {
    WebView webView = new WebView();
    URL estimator = EstimatorApp.class.getResource("/tow-estimator.html");
    if (estimator == null) {
      throw new IllegalStateException("Missing tow-estimator.html resource.");
    }
    webView.getEngine().load(estimator.toExternalForm());

    BorderPane root = new BorderPane(webView);
    Scene scene = new Scene(root, 900, 900);
    stage.setTitle("Southern Pride Towing Estimator");
    stage.setScene(scene);
    stage.show();
  }

  public static void main(String[] args) {
    launch(args);
  }
}
