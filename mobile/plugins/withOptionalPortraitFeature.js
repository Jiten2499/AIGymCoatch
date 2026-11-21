const { withAndroidManifest } = require("@expo/config-plugins");

const portraitFeature = "android.hardware.screen.portrait";

function markPortraitFeatureOptional(androidManifest) {
  const manifest = androidManifest.manifest;
  const features = manifest["uses-feature"] ?? [];
  const existing = features.find((feature) => feature.$["android:name"] === portraitFeature);

  if (existing) {
    existing.$["android:required"] = "false";
  } else {
    features.push({
      $: {
        "android:name": portraitFeature,
        "android:required": "false",
      },
    });
  }

  manifest["uses-feature"] = features;
  return androidManifest;
}

function withOptionalPortraitFeature(config) {
  return withAndroidManifest(config, (config) => {
    config.modResults = markPortraitFeatureOptional(config.modResults);
    return config;
  });
}

module.exports = withOptionalPortraitFeature;
