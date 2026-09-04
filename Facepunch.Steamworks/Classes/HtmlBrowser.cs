using System;
using Steamworks.Data;

namespace Steamworks
{
	/// <summary>
	/// Mouse buttons as understood by <see cref="SteamHTMLSurface"/> (ISteamHTMLSurface::EHTMLMouseButton).
	/// </summary>
	public enum HtmlMouseButton
	{
		Left = 0,
		Right = 1,
		Middle = 2,
	}

	/// <summary>
	/// Modifier keys as understood by <see cref="SteamHTMLSurface"/> (ISteamHTMLSurface::EHTMLKeyModifiers).
	/// </summary>
	[Flags]
	public enum HtmlKeyModifiers
	{
		None = 0,
		Alt = 1 << 0,
		Ctrl = 1 << 1,
		Shift = 1 << 2,
	}

	/// <summary>
	/// Cursor the page wants to show (ISteamHTMLSurface::EMouseCursor).
	/// </summary>
	public enum HtmlMouseCursor
	{
		User = 0, None, Arrow, IBeam, Hourglass, WaitArrow, Crosshair, Up,
		SizeNW, SizeSE, SizeNE, SizeSW, SizeW, SizeE, SizeN, SizeS, SizeWE, SizeNS, SizeAll,
		No, Hand, Blank,
		MiddlePan, NorthPan, NorthEastPan, EastPan, SouthEastPan, SouthPan, SouthWestPan, WestPan, NorthWestPan,
		Alias, Cell, ColResize, CopyCur, VerticalText, RowResize, ZoomIn, ZoomOut, Help, Custom,
	}

	/// <summary>
	/// A frame of rendered page pixels.
	/// </summary>
	/// <remarks>
	/// <see cref="Data"/> is owned by Steam and is only valid for the duration of the
	/// <see cref="HtmlBrowser.OnNeedsPaint"/> callback - Steam frees it as soon as the callback
	/// returns. Copy out of it, don't hold on to it.
	/// </remarks>
	public readonly struct HtmlPaint
	{
		/// <summary>Pointer to <see cref="Width"/> * <see cref="Height"/> BGRA pixels, top row first.</summary>
		public readonly IntPtr Data;

		/// <summary>Width of the whole buffer, in pixels.</summary>
		public readonly int Width;

		/// <summary>Height of the whole buffer, in pixels.</summary>
		public readonly int Height;

		/// <summary>Left edge of the region that actually changed.</summary>
		public readonly int UpdateX;

		/// <summary>Top edge of the region that actually changed.</summary>
		public readonly int UpdateY;

		/// <summary>Width of the region that actually changed.</summary>
		public readonly int UpdateWidth;

		/// <summary>Height of the region that actually changed.</summary>
		public readonly int UpdateHeight;

		/// <summary>Horizontal scroll position of the page when this frame was rendered.</summary>
		public readonly int ScrollX;

		/// <summary>Vertical scroll position of the page when this frame was rendered.</summary>
		public readonly int ScrollY;

		/// <summary>Zoom level the page was rendered at.</summary>
		public readonly float PageScale;

		/// <summary>Increments whenever the page changes, so you can tell frames apart.</summary>
		public readonly uint PageSerial;

		internal HtmlPaint( in HTML_NeedsPaint_t x )
		{
			Data = x.PBGRA;
			Width = (int)x.UnWide;
			Height = (int)x.UnTall;
			UpdateX = (int)x.UnUpdateX;
			UpdateY = (int)x.UnUpdateY;
			UpdateWidth = (int)x.UnUpdateWide;
			UpdateHeight = (int)x.UnUpdateTall;
			ScrollX = (int)x.UnScrollX;
			ScrollY = (int)x.UnScrollY;
			PageScale = x.FlPageScale;
			PageSerial = x.UnPageSerial;
		}

		/// <summary>Size of <see cref="Data"/> in bytes.</summary>
		public int ByteLength => Width * Height * 4;

		/// <summary>Bytes per row of <see cref="Data"/>.</summary>
		public int Stride => Width * 4;
	}

	/// <summary>
	/// A navigation the page wants to start. Set <see cref="Allowed"/> to <see langword="false"/> to block it.
	/// </summary>
	public class HtmlStartRequestEventArgs : EventArgs
	{
		public string Url { get; internal set; }
		public string Target { get; internal set; }
		public string PostData { get; internal set; }
		public bool IsRedirect { get; internal set; }

		/// <summary>Whether the navigation goes ahead. Defaults to <see langword="true"/>.</summary>
		public bool Allowed { get; set; } = true;
	}

	/// <summary>
	/// A javascript alert() or confirm(). The page is blocked until this returns.
	/// </summary>
	public class HtmlDialogEventArgs : EventArgs
	{
		public string Message { get; internal set; }

		/// <summary>What to tell the page. Defaults to <see langword="true"/> (OK).</summary>
		public bool Result { get; set; } = true;
	}

	/// <summary>
	/// The page opened a file picker. The page is blocked until this returns.
	/// </summary>
	public class HtmlFileOpenDialogEventArgs : EventArgs
	{
		public string Title { get; internal set; }
		public string InitialFile { get; internal set; }

		/// <summary>File to hand back, or <see langword="null"/> to cancel.</summary>
		public string SelectedFile { get; set; }
	}

	/// <summary>
	/// Scroll state of one axis of the page.
	/// </summary>
	public readonly struct HtmlScroll
	{
		public readonly uint Max;
		public readonly uint Current;
		public readonly uint PageSize;
		public readonly float PageScale;
		public readonly bool Visible;

		internal HtmlScroll( uint max, uint current, uint pageSize, float pageScale, bool visible )
		{
			Max = max;
			Current = current;
			PageSize = pageSize;
			PageScale = pageScale;
			Visible = visible;
		}
	}

	/// <summary>
	/// An offscreen browser owned by Steam. Get one from <see cref="SteamHTMLSurface.CreateBrowser"/>.
	/// </summary>
	/// <remarks>
	/// Callbacks arrive on whichever thread pumps <see cref="Dispatch"/>, so if you're driving a UI
	/// with this, pump callbacks from the UI thread.
	/// </remarks>
	public class HtmlBrowser : IDisposable
	{
		/// <summary>Steam's handle for this browser. Changes if Steam restarts the html host.</summary>
		public uint Handle { get; private set; }

		/// <summary>False once <see cref="Dispose"/> has been called.</summary>
		public bool IsValid => Handle != 0;

		/// <summary>Size last passed to <see cref="SetSize"/>, in pixels.</summary>
		public int Width { get; private set; }

		/// <summary>Size last passed to <see cref="SetSize"/>, in pixels.</summary>
		public int Height { get; private set; }

		/// <summary>URL currently displayed.</summary>
		public string Url { get; private set; }

		/// <summary>Title of the current page.</summary>
		public string Title { get; private set; }

		public bool CanGoBack { get; private set; }
		public bool CanGoForward { get; private set; }

		/// <summary>A new frame is ready. The pixels are only valid for the duration of this event.</summary>
		public event Action<HtmlPaint> OnNeedsPaint;

		/// <summary>A navigation is about to start, and can be blocked.</summary>
		public event Action<HtmlStartRequestEventArgs> OnStartRequest;

		/// <summary>The displayed URL changed.</summary>
		public event Action<string> OnUrlChanged;

		/// <summary>A page finished loading. Passes url and title.</summary>
		public event Action<string, string> OnFinishedRequest;

		/// <summary>The page title changed.</summary>
		public event Action<string> OnTitleChanged;

		/// <summary>Back/forward availability changed.</summary>
		public event Action<bool, bool> OnCanGoBackAndForwardChanged;

		/// <summary>The page wants to open a URL somewhere else. Unhandled, it loads here instead.</summary>
		public event Action<string> OnOpenLinkInNewTab;

		/// <summary>The page wants a popup window. Unhandled, it loads here instead.</summary>
		public event Action<string> OnNewWindow;

		/// <summary>Javascript alert().</summary>
		public event Action<HtmlDialogEventArgs> OnJSAlert;

		/// <summary>Javascript confirm().</summary>
		public event Action<HtmlDialogEventArgs> OnJSConfirm;

		/// <summary>The page opened a file picker.</summary>
		public event Action<HtmlFileOpenDialogEventArgs> OnFileOpenDialog;

		/// <summary>The page wants a different mouse cursor.</summary>
		public event Action<HtmlMouseCursor> OnSetCursor;

		/// <summary>Status bar text, e.g. the target of a hovered link.</summary>
		public event Action<string> OnStatusText;

		/// <summary>Show a tooltip.</summary>
		public event Action<string> OnShowToolTip;

		/// <summary>Change the text of the visible tooltip.</summary>
		public event Action<string> OnUpdateToolTip;

		/// <summary>Hide the visible tooltip.</summary>
		public event Action OnHideToolTip;

		/// <summary>Result of <see cref="Find"/>: total matches and the current one.</summary>
		public event Action<uint, uint> OnSearchResults;

		/// <summary>Result of <see cref="GetLinkAtPosition"/>: url, is-an-input, is-a-live-link.</summary>
		public event Action<string, bool, bool> OnLinkAtPosition;

		/// <summary>Horizontal scrollbar state changed.</summary>
		public event Action<HtmlScroll> OnHorizontalScroll;

		/// <summary>Vertical scrollbar state changed.</summary>
		public event Action<HtmlScroll> OnVerticalScroll;

		/// <summary>The page called window.close().</summary>
		public event Action OnCloseRequested;

		/// <summary>Steam restarted its html host and gave this browser a new handle.</summary>
		public event Action OnRestarted;

		internal HtmlBrowser( uint handle )
		{
			Handle = handle;
		}

		static ISteamHTMLSurface Internal => SteamHTMLSurface.Internal;

		void ThrowIfInvalid()
		{
			if ( !IsValid )
				throw new ObjectDisposedException( nameof( HtmlBrowser ) );
		}

		/// <summary>Tells Steam how big to render the page, in pixels.</summary>
		public void SetSize( int width, int height )
		{
			ThrowIfInvalid();

			if ( width <= 0 || height <= 0 )
				return;

			if ( width == Width && height == Height )
				return;

			Width = width;
			Height = height;
			Internal.SetSize( Handle, (uint)width, (uint)height );
		}

		/// <summary>Navigates to a URL. Pass <paramref name="postData"/> to POST instead of GET.</summary>
		public void LoadUrl( string url, string postData = null )
		{
			ThrowIfInvalid();
			Internal.LoadURL( Handle, url, postData );
		}

		public void Reload() { ThrowIfInvalid(); Internal.Reload( Handle ); }
		public void StopLoad() { ThrowIfInvalid(); Internal.StopLoad( Handle ); }
		public void GoBack() { ThrowIfInvalid(); Internal.GoBack( Handle ); }
		public void GoForward() { ThrowIfInvalid(); Internal.GoForward( Handle ); }
		public void ViewSource() { ThrowIfInvalid(); Internal.ViewSource( Handle ); }
		public void CopyToClipboard() { ThrowIfInvalid(); Internal.CopyToClipboard( Handle ); }
		public void PasteFromClipboard() { ThrowIfInvalid(); Internal.PasteFromClipboard( Handle ); }
		public void OpenDeveloperTools() { ThrowIfInvalid(); Internal.OpenDeveloperTools( Handle ); }

		/// <summary>Adds a header sent with every request from this browser.</summary>
		public void AddHeader( string key, string value )
		{
			ThrowIfInvalid();
			Internal.AddHeader( Handle, key, value );
		}

		/// <summary>Runs a script in the loaded page.</summary>
		public void ExecuteJavascript( string script )
		{
			ThrowIfInvalid();
			Internal.ExecuteJavascript( Handle, script );
		}

		/// <summary>Searches the page. Call again with <paramref name="currentlyInFind"/> to step through matches.</summary>
		public void Find( string search, bool currentlyInFind = false, bool reverse = false )
		{
			ThrowIfInvalid();
			Internal.Find( Handle, search, currentlyInFind, reverse );
		}

		public void StopFind() { ThrowIfInvalid(); Internal.StopFind( Handle ); }

		/// <summary>Asks what link is at a page position. Answered by <see cref="OnLinkAtPosition"/>.</summary>
		public void GetLinkAtPosition( int x, int y )
		{
			ThrowIfInvalid();
			Internal.GetLinkAtPosition( Handle, x, y );
		}

		#region Input

		/// <summary>Mouse position, in page pixels.</summary>
		public void MouseMove( int x, int y ) { ThrowIfInvalid(); Internal.MouseMove( Handle, x, y ); }

		public void MouseDown( HtmlMouseButton button = HtmlMouseButton.Left )
		{
			ThrowIfInvalid();
			Internal.MouseDown( Handle, (IntPtr)(int)button );
		}

		public void MouseUp( HtmlMouseButton button = HtmlMouseButton.Left )
		{
			ThrowIfInvalid();
			Internal.MouseUp( Handle, (IntPtr)(int)button );
		}

		public void MouseDoubleClick( HtmlMouseButton button = HtmlMouseButton.Left )
		{
			ThrowIfInvalid();
			Internal.MouseDoubleClick( Handle, (IntPtr)(int)button );
		}

		/// <summary><paramref name="delta"/> is in pixels of scroll.</summary>
		public void MouseWheel( int delta ) { ThrowIfInvalid(); Internal.MouseWheel( Handle, delta ); }

		/// <summary>
		/// <paramref name="nativeKeyCode"/> is the OS virtual key code (a Windows VK_ value here).
		/// Set <paramref name="isSystemKey"/> for keys that shouldn't also produce a character.
		/// </summary>
		public void KeyDown( uint nativeKeyCode, HtmlKeyModifiers modifiers = HtmlKeyModifiers.None, bool isSystemKey = false )
		{
			ThrowIfInvalid();
			Internal.KeyDown( Handle, nativeKeyCode, (IntPtr)(int)modifiers, isSystemKey );
		}

		public void KeyUp( uint nativeKeyCode, HtmlKeyModifiers modifiers = HtmlKeyModifiers.None )
		{
			ThrowIfInvalid();
			Internal.KeyUp( Handle, nativeKeyCode, (IntPtr)(int)modifiers );
		}

		/// <summary>Sends a typed character. <paramref name="unicodeChar"/> is a unicode code point.</summary>
		public void KeyChar( uint unicodeChar, HtmlKeyModifiers modifiers = HtmlKeyModifiers.None )
		{
			ThrowIfInvalid();
			Internal.KeyChar( Handle, unicodeChar, (IntPtr)(int)modifiers );
		}

		/// <summary>Tells the page whether it has keyboard focus. Drives text caret behaviour.</summary>
		public void SetKeyFocus( bool hasFocus )
		{
			ThrowIfInvalid();
			Internal.SetKeyFocus( Handle, hasFocus );
		}

		public void SetHorizontalScroll( uint absolutePixelScroll )
		{
			ThrowIfInvalid();
			Internal.SetHorizontalScroll( Handle, absolutePixelScroll );
		}

		public void SetVerticalScroll( uint absolutePixelScroll )
		{
			ThrowIfInvalid();
			Internal.SetVerticalScroll( Handle, absolutePixelScroll );
		}

		#endregion

		/// <summary>Zooms the page, 1.0 being 100%. Zooms around the given page point.</summary>
		public void SetPageScaleFactor( float zoom, int pointX = 0, int pointY = 0 )
		{
			ThrowIfInvalid();
			Internal.SetPageScaleFactor( Handle, zoom, pointX, pointY );
		}

		/// <summary>Throttles javascript and repaint timers when the page isn't visible.</summary>
		public void SetBackgroundMode( bool backgroundMode )
		{
			ThrowIfInvalid();
			Internal.SetBackgroundMode( Handle, backgroundMode );
		}

		/// <summary>Sets the display scaling, e.g. 1.5 for a 150% display.</summary>
		public void SetDpiScalingFactor( float scaling )
		{
			ThrowIfInvalid();
			Internal.SetDPIScalingFactor( Handle, scaling );
		}

		#region Callback plumbing

		internal void InternalOnNeedsPaint( in HTML_NeedsPaint_t x )
		{
			if ( x.PBGRA == IntPtr.Zero )
				return;

			OnNeedsPaint?.Invoke( new HtmlPaint( x ) );
		}

		internal void InternalOnStartRequest( in HTML_StartRequest_t x )
		{
			var args = new HtmlStartRequestEventArgs
			{
				Url = x.PchURL,
				Target = x.PchTarget,
				PostData = x.PchPostData,
				IsRedirect = x.BIsRedirect,
			};

			OnStartRequest?.Invoke( args );

			// Steam blocks the navigation until we answer, handler or not.
			Internal.AllowStartRequest( Handle, args.Allowed );
		}

		internal void InternalOnUrlChanged( in HTML_URLChanged_t x )
		{
			Url = x.PchURL;
			OnUrlChanged?.Invoke( Url );
		}

		internal void InternalOnFinishedRequest( in HTML_FinishedRequest_t x )
		{
			Url = x.PchURL;
			Title = x.PchPageTitle;
			OnFinishedRequest?.Invoke( Url, Title );
		}

		internal void InternalOnChangedTitle( in HTML_ChangedTitle_t x )
		{
			Title = x.PchTitle;
			OnTitleChanged?.Invoke( Title );
		}

		internal void InternalOnCanGoBackAndForward( in HTML_CanGoBackAndForward_t x )
		{
			CanGoBack = x.BCanGoBack;
			CanGoForward = x.BCanGoForward;
			OnCanGoBackAndForwardChanged?.Invoke( CanGoBack, CanGoForward );
		}

		internal void InternalOnOpenLinkInNewTab( in HTML_OpenLinkInNewTab_t x )
		{
			string url = x.PchURL;

			if ( OnOpenLinkInNewTab != null )
				OnOpenLinkInNewTab( url );
			else
				LoadUrl( url );
		}

		internal void InternalOnNewWindow( in HTML_NewWindow_t x )
		{
			// The popup's handle is explicitly not usable (Valve names it _IGNORE), so there's
			// nothing to clean up here - we either hand the url out or follow it ourselves.
			string url = x.PchURL;

			if ( OnNewWindow != null )
				OnNewWindow( url );
			else
				LoadUrl( url );
		}

		internal void InternalOnJSAlert( in HTML_JSAlert_t x )
		{
			var args = new HtmlDialogEventArgs { Message = x.PchMessage };
			OnJSAlert?.Invoke( args );

			// The page is frozen until we respond, handler or not.
			Internal.JSDialogResponse( Handle, args.Result );
		}

		internal void InternalOnJSConfirm( in HTML_JSConfirm_t x )
		{
			var args = new HtmlDialogEventArgs { Message = x.PchMessage };
			OnJSConfirm?.Invoke( args );

			Internal.JSDialogResponse( Handle, args.Result );
		}

		internal void InternalOnFileOpenDialog( in HTML_FileOpenDialog_t x )
		{
			var args = new HtmlFileOpenDialogEventArgs
			{
				Title = x.PchTitle,
				InitialFile = x.PchInitialFile,
			};

			OnFileOpenDialog?.Invoke( args );

			// Unanswered, the page hangs on the file input forever.
			Internal.FileLoadDialogResponse( Handle, args.SelectedFile );
		}

		internal void InternalOnSetCursor( in HTML_SetCursor_t x ) => OnSetCursor?.Invoke( (HtmlMouseCursor)x.EMouseCursor );
		internal void InternalOnStatusText( in HTML_StatusText_t x ) => OnStatusText?.Invoke( x.PchMsg );
		internal void InternalOnShowToolTip( in HTML_ShowToolTip_t x ) => OnShowToolTip?.Invoke( x.PchMsg );
		internal void InternalOnUpdateToolTip( in HTML_UpdateToolTip_t x ) => OnUpdateToolTip?.Invoke( x.PchMsg );
		internal void InternalOnHideToolTip( in HTML_HideToolTip_t x ) => OnHideToolTip?.Invoke();
		internal void InternalOnSearchResults( in HTML_SearchResults_t x ) => OnSearchResults?.Invoke( x.UnResults, x.UnCurrentMatch );

		internal void InternalOnLinkAtPosition( in HTML_LinkAtPosition_t x )
			=> OnLinkAtPosition?.Invoke( x.PchURL, x.BInput, x.BLiveLink );

		internal void InternalOnHorizontalScroll( in HTML_HorizontalScroll_t x )
			=> OnHorizontalScroll?.Invoke( new HtmlScroll( x.UnScrollMax, x.UnScrollCurrent, x.UnPageSize, x.FlPageScale, x.BVisible ) );

		internal void InternalOnVerticalScroll( in HTML_VerticalScroll_t x )
			=> OnVerticalScroll?.Invoke( new HtmlScroll( x.UnScrollMax, x.UnScrollCurrent, x.UnPageSize, x.FlPageScale, x.BVisible ) );

		internal void InternalOnCloseBrowser( in HTML_CloseBrowser_t x ) => OnCloseRequested?.Invoke();

		internal void InternalOnRestarted( uint newHandle )
		{
			Handle = newHandle;

			// The new browser starts out with no size, so put it back where it was.
			if ( Width > 0 && Height > 0 )
				Internal.SetSize( Handle, (uint)Width, (uint)Height );

			OnRestarted?.Invoke();
		}

		#endregion

		/// <summary>Destroys the browser and frees Steam's resources for it.</summary>
		public void Dispose()
		{
			if ( !IsValid )
				return;

			var handle = Handle;
			Handle = 0;

			SteamHTMLSurface.Unregister( handle );
			Internal?.RemoveBrowser( handle );
		}
	}
}
