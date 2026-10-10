      // ---- InventoryTracking hook (DEBUG build: shows each step on screen) ----
      private var itDbg:flash.text.TextField;
      private var itDbgLines:Array = [];

      // One message per line, newest at the bottom, box grows downward and wraps long lines (last 16 lines kept).
      public function itDbgMsg(message:String) : void
      {
         try
         {
            var format:flash.text.TextFormat = new flash.text.TextFormat("$MAIN_Font",18,0xFFFF00);
            if(itDbg == null)
            {
               itDbg = new flash.text.TextField();
               itDbg.embedFonts = true;
               itDbg.defaultTextFormat = format;
               itDbg.background = true;
               itDbg.backgroundColor = 0x000000;
               itDbg.multiline = true;
               itDbg.wordWrap = true;
               itDbg.autoSize = flash.text.TextFieldAutoSize.LEFT;
               itDbg.width = 1100;
               itDbg.x = 20;
               itDbg.y = 130;
               itDbg.selectable = false;
               itDbg.mouseEnabled = false;
               stage.addChild(itDbg);
            }
            itDbgLines.push(message);
            if(itDbgLines.length > 16) itDbgLines.shift();
            itDbg.text = itDbgLines.join("\n");
            itDbg.setTextFormat(format);
         }
         catch(e:Error) {}
      }
      public function itDbgClass(name:String) : Object
      {
         if(name == "Shared.AS3.Data.BSUIDataManager") return Shared.AS3.Data.BSUIDataManager;
         if(name == "Shared.AS3.Events.CustomEvent") return Shared.AS3.Events.CustomEvent;
         return null;
      }

      private function loadInventoryTracking() : void
      {
         try
         {
            itDbgMsg("hook");
            var itLoader:flash.display.Loader = new flash.display.Loader();
            itLoader.contentLoaderInfo.addEventListener(flash.events.IOErrorEvent.IO_ERROR,ignoreInventoryTrackingError);
            itLoader.contentLoaderInfo.addEventListener(flash.events.Event.COMPLETE,itDbgComplete);
            itLoader.uncaughtErrorEvents.addEventListener(flash.events.UncaughtErrorEvent.UNCAUGHT_ERROR,itDbgUncaught);
            itLoader.load(new flash.net.URLRequest("InventoryTracking.swf"),new flash.system.LoaderContext(false,flash.system.ApplicationDomain.currentDomain));
            addChild(itLoader);
         }
         catch(e:Error) { itDbgMsg("hook error " + e); }
      }

      private function ignoreInventoryTrackingError(event:flash.events.Event) : void
      {
         itDbgMsg("load error " + event);
      }

      private function itDbgComplete(event:flash.events.Event) : void
      {
         itDbgMsg("loaded");
      }

      private function itDbgUncaught(event:flash.events.UncaughtErrorEvent) : void
      {
         itDbgMsg("uncaught " + event.error);
      }