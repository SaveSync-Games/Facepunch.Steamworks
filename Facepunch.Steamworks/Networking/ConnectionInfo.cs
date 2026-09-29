using System.Runtime.InteropServices;

namespace Steamworks.Data
{
	/// <summary>
	/// Describe the state of a connection
	/// </summary>
	[StructLayout( LayoutKind.Sequential, Size = 696 )]
	public struct ConnectionInfo
	{
		internal NetIdentity identity;
		internal long userData;
		internal Socket listenSocket;
		internal NetAddress address;
		internal ushort pad;
		internal SteamNetworkingPOPID popRemote;
		internal SteamNetworkingPOPID popRelay;
		internal ConnectionState state;
		internal int endReason;
		[MarshalAs( UnmanagedType.ByValTStr, SizeConst = 128 )]
		internal string endDebug;
		[MarshalAs( UnmanagedType.ByValTStr, SizeConst = 128 )]
		internal string connectionDescription;
		internal int flags;

		/// <summary>
		/// High level state of the connection
		/// </summary>
		public ConnectionState State => state;

		/// <summary>
		/// Remote address.  Might be all 0's if we don't know it, or if this is N/A.
		/// </summary>
		public NetAddress Address => address;

		/// <summary>
		/// Who is on the other end?  Depending on the connection type and phase of the connection, we might not know
		/// </summary>
		public NetIdentity Identity => identity;

		/// <summary>
		/// Basic cause of the connection termination or problem.
		/// </summary>
		public NetConnectionEnd EndReason => (NetConnectionEnd)endReason;

		/// <summary>
		/// Misc flags - a bitmask of k_nSteamNetworkConnectionInfoFlags_Xxxx.
		/// </summary>
		public int Flags => flags;

		/// <summary>
		/// The connection is relayed somehow (SDR or TURN) rather than direct.
		/// </summary>
		public bool IsRelayed => ( flags & 16 ) != 0;

		/// <summary>
		/// Code of the relay data center carrying the connection, or null if it isn't relayed.
		/// </summary>
		public string RelayPop => PopToString( popRelay );

		/// <summary>
		/// Code of the data center the remote host is in, or null if we don't know.
		/// </summary>
		public string RemotePop => PopToString( popRemote );

		// Mirrors GetSteamNetworkingLocationPOPStringFromID: three chars in the low 24 bits, and an
		// optional fourth in the top byte.
		static string PopToString( SteamNetworkingPOPID pop )
		{
			uint id = pop;
			if ( id == 0 ) return null;

			var code = new string( new[] { (char)( ( id >> 16 ) & 0xFF ), (char)( ( id >> 8 ) & 0xFF ), (char)( id & 0xFF ), (char)( id >> 24 ) } );
			return code.TrimEnd( '\0' );
		}
	}
}