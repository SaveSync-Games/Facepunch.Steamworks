using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks.Data;

namespace Steamworks
{
	/// <summary>
	/// Steam's embedded browser. Pages are rendered offscreen by Steam and handed back to you
	/// as raw BGRA frames via <see cref="HtmlBrowser.OnNeedsPaint"/>, which you're expected to
	/// blit into a texture/bitmap yourself.
	/// </summary>
	public class SteamHTMLSurface : SteamClientClass<SteamHTMLSurface>
	{
		internal static ISteamHTMLSurface Internal => Interface as ISteamHTMLSurface;

		/// <summary>
		/// Live browsers, keyed by their Steam handle, so the global callbacks below can be
		/// routed to the browser they belong to.
		/// </summary>
		static readonly Dictionary<uint, HtmlBrowser> browsers = new Dictionary<uint, HtmlBrowser>();

		static bool surfaceInitialized;

		internal override bool InitializeInterface( bool server )
		{
			SetInterface( server, new ISteamHTMLSurface( server ) );
			if ( Interface.Self == IntPtr.Zero ) return false;

			InstallEvents();

			return true;
		}

		internal override void DestroyInterface( bool server )
		{
			foreach ( var browser in new List<HtmlBrowser>( browsers.Values ) )
			{
				browser.Dispose();
			}

			browsers.Clear();

			if ( surfaceInitialized )
			{
				Internal?.Shutdown();
				surfaceInitialized = false;
			}

			base.DestroyInterface( server );
		}

		internal static void InstallEvents()
		{
			// Every callback carries the browser handle, so dispatch to the owning browser.
			Dispatch.Install<HTML_NeedsPaint_t>( x => Find( x.UnBrowserHandle )?.InternalOnNeedsPaint( x ) );
			Dispatch.Install<HTML_StartRequest_t>( x => Find( x.UnBrowserHandle )?.InternalOnStartRequest( x ) );
			Dispatch.Install<HTML_URLChanged_t>( x => Find( x.UnBrowserHandle )?.InternalOnUrlChanged( x ) );
			Dispatch.Install<HTML_FinishedRequest_t>( x => Find( x.UnBrowserHandle )?.InternalOnFinishedRequest( x ) );
			Dispatch.Install<HTML_ChangedTitle_t>( x => Find( x.UnBrowserHandle )?.InternalOnChangedTitle( x ) );
			Dispatch.Install<HTML_CanGoBackAndForward_t>( x => Find( x.UnBrowserHandle )?.InternalOnCanGoBackAndForward( x ) );
			Dispatch.Install<HTML_OpenLinkInNewTab_t>( x => Find( x.UnBrowserHandle )?.InternalOnOpenLinkInNewTab( x ) );
			Dispatch.Install<HTML_NewWindow_t>( x => Find( x.UnBrowserHandle )?.InternalOnNewWindow( x ) );
			Dispatch.Install<HTML_JSAlert_t>( x => Find( x.UnBrowserHandle )?.InternalOnJSAlert( x ) );
			Dispatch.Install<HTML_JSConfirm_t>( x => Find( x.UnBrowserHandle )?.InternalOnJSConfirm( x ) );
			Dispatch.Install<HTML_FileOpenDialog_t>( x => Find( x.UnBrowserHandle )?.InternalOnFileOpenDialog( x ) );
			Dispatch.Install<HTML_SetCursor_t>( x => Find( x.UnBrowserHandle )?.InternalOnSetCursor( x ) );
			Dispatch.Install<HTML_StatusText_t>( x => Find( x.UnBrowserHandle )?.InternalOnStatusText( x ) );
			Dispatch.Install<HTML_ShowToolTip_t>( x => Find( x.UnBrowserHandle )?.InternalOnShowToolTip( x ) );
			Dispatch.Install<HTML_UpdateToolTip_t>( x => Find( x.UnBrowserHandle )?.InternalOnUpdateToolTip( x ) );
			Dispatch.Install<HTML_HideToolTip_t>( x => Find( x.UnBrowserHandle )?.InternalOnHideToolTip( x ) );
			Dispatch.Install<HTML_SearchResults_t>( x => Find( x.UnBrowserHandle )?.InternalOnSearchResults( x ) );
			Dispatch.Install<HTML_LinkAtPosition_t>( x => Find( x.UnBrowserHandle )?.InternalOnLinkAtPosition( x ) );
			Dispatch.Install<HTML_HorizontalScroll_t>( x => Find( x.UnBrowserHandle )?.InternalOnHorizontalScroll( x ) );
			Dispatch.Install<HTML_VerticalScroll_t>( x => Find( x.UnBrowserHandle )?.InternalOnVerticalScroll( x ) );
			Dispatch.Install<HTML_CloseBrowser_t>( x => Find( x.UnBrowserHandle )?.InternalOnCloseBrowser( x ) );

			// Steam can restart the html host process out from under us, which hands the
			// browser a brand new handle. Re-key it or every later callback goes nowhere.
			Dispatch.Install<HTML_BrowserRestarted_t>( x =>
			{
				var browser = Find( x.UnOldBrowserHandle );
				if ( browser == null ) return;

				browsers.Remove( x.UnOldBrowserHandle );
				browser.InternalOnRestarted( x.UnBrowserHandle );
				browsers[x.UnBrowserHandle] = browser;
			} );
		}

		static HtmlBrowser Find( uint handle )
		{
			return browsers.TryGetValue( handle, out var browser ) ? browser : null;
		}

		internal static void Register( HtmlBrowser browser ) => browsers[browser.Handle] = browser;
		internal static void Unregister( uint handle ) => browsers.Remove( handle );

		/// <summary>
		/// Brings up Steam's html host process. Safe to call repeatedly - only the first call does
		/// anything. Called for you by <see cref="CreateBrowser"/>.
		/// </summary>
		public static bool Init()
		{
			SteamClient.ValidCheck();

			if ( surfaceInitialized )
				return true;

			if ( Internal == null )
				return false;

			surfaceInitialized = Internal.Init();
			return surfaceInitialized;
		}

		/// <summary>
		/// Creates an offscreen browser. Returns <see langword="null"/> if Steam couldn't
		/// create one - usually because the html surface isn't available.
		/// </summary>
		/// <param name="userAgent">Appended to Steam's user agent string, or <see langword="null"/>.</param>
		/// <param name="userCss">A stylesheet applied to every page, or <see langword="null"/>.</param>
		public static async Task<HtmlBrowser> CreateBrowser( string userAgent = null, string userCss = null )
		{
			if ( !Init() )
				return null;

			var result = await Internal.CreateBrowser( userAgent, userCss );
			if ( !result.HasValue || result.Value.UnBrowserHandle == 0 )
				return null;

			var browser = new HtmlBrowser( result.Value.UnBrowserHandle );
			Register( browser );

			return browser;
		}

		/// <summary>
		/// Sets a cookie on Steam's browser cookie jar, shared by every browser.
		/// </summary>
		public static void SetCookie( string hostname, string key, string value, string path = "/", DateTime? expires = null, bool secure = false, bool httpOnly = false )
		{
			Internal.SetCookie( hostname, key, value, path, expires.HasValue ? Epoch.FromDateTime( expires.Value ) : 0, secure, httpOnly );
		}
	}
}
