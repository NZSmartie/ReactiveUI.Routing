// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//

using Foundation;
using UIKit;

namespace ShareNavigation.Views
{
	[Register ("TestViewController")]
	partial class PhotoListViewController
	{
		[Outlet]
		UILabel TheGuid { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (TheGuid != null) {
				TheGuid.Dispose ();
				TheGuid = null;
			}
		}
	}
}
