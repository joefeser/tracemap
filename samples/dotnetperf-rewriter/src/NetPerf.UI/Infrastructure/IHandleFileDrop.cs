
namespace MetadataDashboard.NetperfUI.Infrastructure {
	internal interface IHandleFileDrop {

		bool CanAcceptFileDrop(string[] files);
		void AcceptFileDrop(string[] files);
	}
}
