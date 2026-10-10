      // ---- InventoryTracking hook: loads the optional add-on interface/InventoryTracking.swf (nothing happens if the file is missing) ----
      private function loadInventoryTracking() : void
      {
         try
         {
            var itLoader:flash.display.Loader = new flash.display.Loader();
            itLoader.contentLoaderInfo.addEventListener(flash.events.IOErrorEvent.IO_ERROR,ignoreInventoryTrackingError);
            itLoader.load(new flash.net.URLRequest("InventoryTracking.swf"),new flash.system.LoaderContext(false,flash.system.ApplicationDomain.currentDomain));
            addChild(itLoader);
         }
         catch(e:Error) {}
      }

      private function ignoreInventoryTrackingError(event:flash.events.Event) : void
      {
      }
