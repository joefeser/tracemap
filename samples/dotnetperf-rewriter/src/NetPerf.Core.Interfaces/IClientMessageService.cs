
namespace NetPerf.Core.Interfaces {

	/// <summary>
	/// Interface that is used by the IOC container to allow multiple implementations of the client message service
	/// </summary>
	/// <remarks>
	/// For testing we use an in memory model but for the real service we use a Named pipe. This interface is used to describe that contract.
	/// </remarks>
	public interface IClientMessageService {

		//The new clients will know what to do with this, for now it is just for the new SqLite Client
		void RegisterMetadata(string assemblyName, string ilMetadata);

		void EnterMethod(int moduleId, int methodId);

		void EnterGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes);

		void ExitMethod(int moduleId, int methodId);

		void ExitGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes);

		void InstrumentationPointHit(int moduleId, int pointId);

		/// <summary>
		/// When the domain unloads this needs to be called.
		/// </summary>
		void Stop();
	}
}
