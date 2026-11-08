function configureAdapter() {
    this.value();
    this['service'](selectFeature(), function () {});
    registry.hooks.service(featureName, function () {});
}
