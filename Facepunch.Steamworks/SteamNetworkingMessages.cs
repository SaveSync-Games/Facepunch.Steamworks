using System;
using System.Runtime.InteropServices;
using Steamworks.Data;

namespace Steamworks
{
	/// <summary>
	/// Connectionless messaging over Steam's relay network - the successor to <see cref="SteamNetworking"/>'s
	/// P2P packets. Addressed by identity and channel, with the session to each peer set up on demand.
	/// </summary>
	public class SteamNetworkingMessages : SteamSharedClass<SteamNetworkingMessages>
	{
		internal static ISteamNetworkingMessages Internal => Interface as ISteamNetworkingMessages;

		internal override bool InitializeInterface( bool server )
		{
			SetInterface( server, new ISteamNetworkingMessages( server ) );
			if ( Interface.Self == IntPtr.Zero ) return false;

			InstallEvents( server );

			return true;
		}

		internal static void InstallEvents( bool server )
		{
			Dispatch.Install<SteamNetworkingMessagesSessionRequest_t>( x => OnSessionRequest?.Invoke( x.DentityRemote ), server );
			Dispatch.Install<SteamNetworkingMessagesSessionFailed_t>( x => OnSessionFailed?.Invoke( x.Nfo ), server );
		}

		/// <summary>
		/// Invoked when a peer we have no session with sends us a message. Respond with
		/// <see cref="AcceptSessionWithUser(NetIdentity)"/> to receive it - otherwise the session is
		/// dropped along with anything sent on it. Sending to a peer accepts their session implicitly.
		/// </summary>
		public static Action<NetIdentity> OnSessionRequest;

		/// <summary>
		/// Invoked when a session fails, or can't be established. Messages still queued to that peer
		/// are dropped.
		/// </summary>
		public static Action<ConnectionInfo> OnSessionFailed;

		/// <summary>
		/// Send <paramref name="size"/> bytes at <paramref name="ptr"/> to <paramref name="identity"/> on
		/// <paramref name="channel"/>. The data is copied, so the buffer is free to reuse on return.
		/// </summary>
		public static Result SendMessageToUser( NetIdentity identity, IntPtr ptr, int size, SendType sendType = SendType.Reliable, int channel = 0 )
		{
			return Internal.SendMessageToUser( ref identity, ptr, (uint)size, (int)sendType, channel );
		}

		/// <summary>
		/// Send <paramref name="length"/> bytes of <paramref name="data"/> from <paramref name="offset"/>
		/// to <paramref name="identity"/> on <paramref name="channel"/>.
		/// </summary>
		public static unsafe Result SendMessageToUser( NetIdentity identity, byte[] data, int offset, int length, SendType sendType = SendType.Reliable, int channel = 0 )
		{
			if ( offset < 0 || length < 0 || offset + length > data.Length )
				throw new ArgumentOutOfRangeException( nameof( length ) );

			fixed ( byte* ptr = data )
			{
				return SendMessageToUser( identity, (IntPtr)(ptr + offset), length, sendType, channel );
			}
		}

		/// <summary>
		/// Send all of <paramref name="data"/> to <paramref name="identity"/> on <paramref name="channel"/>.
		/// </summary>
		public static Result SendMessageToUser( NetIdentity identity, byte[] data, SendType sendType = SendType.Reliable, int channel = 0 )
		{
			return SendMessageToUser( identity, data, 0, data.Length, sendType, channel );
		}

		/// <summary>
		/// Take the next message queued on <paramref name="channel"/>, if there is one, copying it into
		/// <paramref name="buffer"/>. <paramref name="size"/> is how many bytes were copied - a message
		/// larger than the buffer is truncated to fit. Returns false when the channel is empty.
		/// </summary>
		public static unsafe bool ReceiveMessageOnChannel( int channel, byte[] buffer, out int size, out NetIdentity identity )
		{
			NetMsg* msg = null;
			if ( Internal.ReceiveMessagesOnChannel( channel, new IntPtr( &msg ), 1 ) <= 0 || msg == null )
			{
				size = 0;
				identity = default;
				return false;
			}

			try
			{
				size = Math.Min( msg->DataSize, buffer.Length );
				Marshal.Copy( msg->DataPtr, buffer, 0, size );
				identity = msg->Identity;
			}
			finally
			{
				NetMsg.InternalRelease( msg );
			}

			return true;
		}

		/// <summary>
		/// Accept a session <paramref name="identity"/> asked for - the answer to <see cref="OnSessionRequest"/>.
		/// </summary>
		public static bool AcceptSessionWithUser( NetIdentity identity ) => Internal.AcceptSessionWithUser( ref identity );

		/// <summary>
		/// Close the session with <paramref name="identity"/>, dropping anything queued either way.
		/// </summary>
		public static bool CloseSessionWithUser( NetIdentity identity ) => Internal.CloseSessionWithUser( ref identity );

		/// <summary>
		/// Close one channel with <paramref name="identity"/>. The session itself closes once no
		/// channel is left open on it.
		/// </summary>
		public static bool CloseChannelWithUser( NetIdentity identity, int channel ) => Internal.CloseChannelWithUser( ref identity, channel );

		/// <summary>
		/// The state of the session with <paramref name="identity"/>, plus its details and live stats.
		/// </summary>
		public static ConnectionState GetSessionConnectionInfo( NetIdentity identity, out ConnectionInfo info, out ConnectionStatus status )
		{
			info = default;
			status = default;
			return Internal.GetSessionConnectionInfo( ref identity, ref info, ref status );
		}
	}
}
