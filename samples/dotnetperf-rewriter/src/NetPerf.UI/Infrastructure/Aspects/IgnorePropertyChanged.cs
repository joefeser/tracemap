using System;

namespace MetadataDashboard.NetperfUI.Infrastructure.Aspects {

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Interface)]
	internal class IgnorePropertyChangedAttribute : Attribute {
	
	}

}