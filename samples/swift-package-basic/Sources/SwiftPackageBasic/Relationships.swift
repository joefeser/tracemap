public protocol FeatureRendering {
    func render(_ feature: Feature) -> String
}

public protocol NamedFeatureRendering: FeatureRendering {}

open class BaseFeatureRenderer {
    public init() {}

    open func render(_ feature: Feature) -> String {
        feature.name
    }
}

public final class DefaultFeatureRenderer: BaseFeatureRenderer, NamedFeatureRendering {
    public override func render(_ feature: Feature) -> String {
        feature.name
    }
}

