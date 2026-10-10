      // ---- InventoryTracking exporter (injected into vanilla SecureTrade) ----
      public var __SFCodeObj:Object = new Object();
      private var itAlive:Boolean = false;
      private var itFormat:flash.text.TextFormat;
      private var itEmbed:Boolean = true;
      private var itBtnPlayer:flash.text.TextField;
      private var itBtnBox:flash.text.TextField;
      private var itBtnPart:flash.text.TextField;
      private var itBtnVer:flash.text.TextField;
      // Shown (not clickable) next to the buttons so a screenshot proves which build is running.
      // Bump it for every build that is deployed.
      private static const IT_VERSION:String = "IT v1.3";
      private var itStatus:flash.text.TextField;
      private var itPollTimer:uint = 0;
      private var itCardTimer:uint = 0;
      private var itBusy:Boolean = false;
      private var itDone:Boolean = false;
      private var itPart:int = 1;
      private var itMode:int = -1;
      private var itTitle:String = "";
      private var itSrc:String = "";
      private var itSorted:Array = [];
      private var itQueue:Array = [];
      private var itQueuePos:int = 0;
      private var itCards:Object = {};
      private var itCardWait:Number = -1;
      private var itCardRetry:Boolean = false;
      private var itCardSub:Boolean = false;
      private var itOrigId:Number = 0;
      private var itOrigFrom:Boolean = false;
      private var itHasOrig:Boolean = false;
      private static var itB64Chars:String = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

      private function itInit() : void
      {
         if(!itAlive || stage == null) return;
         if(itBtnPlayer != null) return;
         itGuardOn();
         itFormat = new flash.text.TextFormat();
         itFormat.font = "$MAIN_Font";
         try
         {
            var template:flash.text.TextField = this.PlayerInventory_mc.Header_mc.Header_tf;
            itFormat = template.getTextFormat();
            itEmbed = template.embedFonts;
         }
         catch(e:Error) { itEmbed = true; }
         itFormat.size = 20;
         itFormat.color = 0xFFFFFF;
         itFormat.align = "left";
         itFormat.bold = false;
         itFormat.italic = false;
         itFormat.letterSpacing = 0;
         itBtnPlayer = itMakeButton(20,20,340,itPlayerClick);
         itBtnBox = itMakeButton(370,20,300,itBoxClick);
         itBtnPart = itMakeButton(680,20,130,itPartClick);
         itBtnVer = itMakeButton(820,20,110,null);
         itBtnVer.mouseEnabled = false;
         itStatus = itMakeButton(20,62,790,null);
         itStatus.multiline = true;
         itStatus.wordWrap = true;
         itStatus.height = 64;
         itStatus.backgroundColor = 0x101820;
         addEventListener(flash.events.Event.REMOVED_FROM_STAGE,itClose);
         itSetText(itStatus,"");
         itRefresh();
      }

      // Selection guard. While the quantity/price dialog is open the vanilla menu is expected to lock the
      // item lists, but with this mod loaded hovering another row moved the selection and the transfer
      // then used that row instead of the clicked one. Mouse events aimed at anything outside the open
      // dialog are swallowed in the capture phase, so the list never sees them.
      private var itGuardActive:Boolean = false;

      private function itGuardOn() : void
      {
         if(itGuardActive || stage == null) return;
         itGuardActive = true;
         addEventListener(flash.events.Event.ENTER_FRAME,itFrame);
         var types:Array = [flash.events.MouseEvent.MOUSE_MOVE,flash.events.MouseEvent.MOUSE_OVER,flash.events.MouseEvent.ROLL_OVER,
                            flash.events.MouseEvent.MOUSE_DOWN,flash.events.MouseEvent.MOUSE_UP,flash.events.MouseEvent.CLICK];
         var i:int = 0;
         while(i < types.length)
         {
            stage.addEventListener(types[i],itGuard,true,2147483647,false);
            i++;
         }
      }

      // Modal state watch. With this mod loaded the menu stayed dark after a transfer (and was not greyed
      // while the dialog was open). On every open/close of the quantity or price dialog the game's own
      // updateModalActive() is run again (this also locks the item lists while the dialog is open).
      private var itWasOpen:Boolean = false;

      private function itFrame(event:flash.events.Event) : void
      {
         if(!itAlive || itBusy) return;
         var open:Boolean = itModalOpen() != null;
         if(open == itWasOpen) return;
         itWasOpen = open;
         try { updateModalActive(); } catch(e:Error) {}
         // With this mod loaded the playhead stops on the first frame of the modalFadeOut (50) /
         // modalFadeIn (60) animation instead of playing it, leaving the menu light while the dialog is
         // open and grey afterwards. Jump to the last frame of the animation (stop frames 59 / 69).
         gotoAndStop(open ? 59 : 69);
      }

      private function itGuardOff() : void
      {
         if(!itGuardActive || stage == null) return;
         itGuardActive = false;
         removeEventListener(flash.events.Event.ENTER_FRAME,itFrame);
         var types:Array = [flash.events.MouseEvent.MOUSE_MOVE,flash.events.MouseEvent.MOUSE_OVER,flash.events.MouseEvent.ROLL_OVER,
                            flash.events.MouseEvent.MOUSE_DOWN,flash.events.MouseEvent.MOUSE_UP,flash.events.MouseEvent.CLICK];
         var i:int = 0;
         while(i < types.length)
         {
            stage.removeEventListener(types[i],itGuard,true);
            i++;
         }
      }

      private function itModalOpen() : flash.display.DisplayObject
      {
         try
         {
            if(this.ModalSetQuantity_mc != null && this.ModalSetQuantity_mc.opened) return this.ModalSetQuantity_mc;
            if(this.ModalSetPrice_mc != null && this.ModalSetPrice_mc.opened) return this.ModalSetPrice_mc;
         }
         catch(e:Error) {}
         return null;
      }

      private function itGuard(event:flash.events.MouseEvent) : void
      {
         if(!itAlive || itBusy) return;
         var modal:flash.display.DisplayObject = itModalOpen();
         if(modal == null) return;
         var node:flash.display.DisplayObject = event.target as flash.display.DisplayObject;
         while(node != null)
         {
            if(node == modal) return;
            node = node.parent;
         }
         event.stopImmediatePropagation();
      }

      private function itMakeButton(x:Number, y:Number, w:Number, handler:Function) : flash.text.TextField
      {
         var b:flash.text.TextField = new flash.text.TextField();
         b.embedFonts = itEmbed;
         b.defaultTextFormat = itFormat;
         b.background = true;
         b.backgroundColor = 0x203040;
         b.border = true;
         b.borderColor = 0x66CCFF;
         b.width = w;
         b.height = 34;
         b.x = x;
         b.y = y;
         b.selectable = false;
         b.multiline = false;
         b.wordWrap = false;
         b.visible = false;
         if(handler != null) b.addEventListener(flash.events.MouseEvent.CLICK,handler);
         stage.addChild(b);
         return b;
      }

      private function itSetText(b:flash.text.TextField, label:String) : void
      {
         if(b == null) return;
         b.text = label;
         b.setTextFormat(itFormat);
         b.textColor = 0xFFFFFF;
      }

      // Readable name for the on-screen "Exported ..." message.
      private function itDisplayName(src:String) : String
      {
         if(src == "player") return "Player inventory";
         if(itMode == 6) return "Stash";
         if(itMode == 7) return "Scrap box";
         if(itMode == 8) return "Ammo box";
         if(itMode == 9) return "Aid box";
         return "Box";
      }

      private function itStorageLabel(src:String) : String
      {
         if(src == "player") return "player";
         if(itMode == 6) return "stash";
         if(itMode == 7) return "scrap";
         if(itMode == 8) return "ammo";
         if(itMode == 9) return "aid";
         return "mode" + itMode;
      }

      private function itBoxCaption() : String
      {
         if(itMode == 6) return "Export stash";
         if(itMode == 7) return "Export scrap box";
         if(itMode == 8) return "Export ammo box";
         if(itMode == 9) return "Export aid box";
         return "Export";
      }

      private function itReadContext() : Boolean
      {
         itMode = -1;
         itTitle = "";
         try
         {
            var co:Object = BSUIDataManager.GetDataFromClient("ContainerOptionsData").data;
            if(co == null || co["isWorkbench"] || co["isWorkshop"]) return false;
            itMode = int(co["storageMode"]);
         }
         catch(e:Error) { return false; }
         try { itTitle = String(BSUIDataManager.GetDataFromClient("OtherInventoryTypeData").data["defaultHeaderText"]); } catch(e:Error) {}
         if(m_CorpseLootMode) return false;
         return itMode == 6 || itMode == 7 || itMode == 8 || itMode == 9;
      }

      // Re-evaluated on a timer; buttons exist only in full storage menus.
      private function itRefresh() : void
      {
         if(!itAlive) return;
         var ok:Boolean = itReadContext();
         if(itBtnPlayer != null)
         {
            itBtnPlayer.visible = ok && itMode == 6;
            itBtnBox.visible = ok;
            itBtnPart.visible = ok;
            itBtnVer.visible = ok;
            itStatus.visible = ok && itStatus.text.length > 0;
            if(!itBusy)
            {
               itSetText(itBtnPlayer,"Export player inventory");
               itSetText(itBtnBox,itBoxCaption());
               itSetText(itBtnPart,"Part " + itPart);
               itSetText(itBtnVer,IT_VERSION);
            }
         }
         itPollTimer = setTimeout(itRefresh,700);
      }

      private function itStatusMsg(message:String) : void
      {
         itSetText(itStatus,message);
         itStatus.visible = true;
      }

      private function itPartClick(event:flash.events.MouseEvent) : void
      {
         event.stopImmediatePropagation();
         if(!itAlive || itBusy) return;
         itPart = itPart % 8 + 1;
         itSetText(itBtnPart,"Part " + itPart);
      }

      private function itPlayerClick(event:flash.events.MouseEvent) : void
      {
         event.stopImmediatePropagation();
         itStart("player");
      }

      private function itBoxClick(event:flash.events.MouseEvent) : void
      {
         event.stopImmediatePropagation();
         itStart("other");
      }

      private function itGetList(src:String) : Array
      {
         try
         {
            var d:Object = BSUIDataManager.GetDataFromClient(src == "player" ? "PlayerInventoryData" : "OtherInventoryData").data;
            return d["InventoryList"] as Array;
         }
         catch(e:Error) {}
         return null;
      }

      private function itStart(src:String) : void
      {
         if(!itAlive || itBusy) return;
         if(itDone) { itStatusMsg("An export was already saved in this menu session. Close and reopen the menu for another export or part."); return; }
         if(!itReadContext() || (src == "player" && itMode != 6)) { itStatusMsg("Export is not available in this menu."); return; }
         var list:Array = itGetList(src);
         if(list == null) { itStatusMsg("Inventory data is not available yet."); return; }
         itBusy = true;
         itSrc = src;
         itSetText(itBtnPlayer,"Working...");
         itSetText(itBtnBox,"Working...");
         itSorted = list.concat();
         itSorted.sortOn("serverHandleID",Array.NUMERIC);
         itCards = {};
         itQueue = [];
         var seenMods:Object = {};
         var i:int = 0;
         while(i < itSorted.length)
         {
            var row0:Object = itSorted[i];
            if(row0["isLegendary"]) itQueue.push(row0);
            else if(itIsLegMod(row0))
            {
               // Identical legendary modules share one card read.
               var modKey:String = itCardKey(row0);
               if(seenMods[modKey] == null) { seenMods[modKey] = true; itQueue.push(row0); }
            }
            i++;
         }
         itQueuePos = 0;
         itHasOrig = false;
         try
         {
            var cur:Object = this.selectedListEntry;
            if(cur != null && cur["serverHandleID"] != null)
            {
               itOrigId = Number(cur["serverHandleID"]);
               itOrigFrom = this.selectedList != this.PlayerInventory_mc;
               itHasOrig = true;
            }
         }
         catch(e:Error) {}
         if(itQueue.length > 0 && !itCardSub)
         {
            BSUIDataManager.Subscribe("InventoryItemCardData",itCardData);
            itCardSub = true;
         }
         itStatusMsg("Reading " + itSorted.length + " items; " + itQueue.length + " item cards...");
         itCardNext();
      }

      // Legendary modules (mod boxes) are not flagged legendary by the game: the number of stars is the number
      // of leading ¬ characters in their name (filter bit 32768 = mods), e.g. "¬¬¬ Durability".
      private function itIsLegMod(r:Object) : Boolean
      {
         var n:String = r["text"] == null ? "" : String(r["text"]);
         return n.length > 0 && n.charCodeAt(0) == 172 && (Number(r["filterFlag"]) & 32768) != 0;
      }

      private function itNeedsCard(r:Object) : Boolean
      {
         return Boolean(r["isLegendary"]) || itIsLegMod(r);
      }

      // Cards are stored per item handle, except legendary modules, which share one card per name.
      private function itCardKey(r:Object) : String
      {
         return r["isLegendary"] ? String(r["serverHandleID"]) : "mod:" + String(r["text"]);
      }

      private function itCardNext() : void
      {
         if(!itAlive) return;
         if(itQueuePos >= itQueue.length) { itFinishCards(); return; }
         var row:Object = itQueue[itQueuePos];
         itCardWait = Number(row["serverHandleID"]);
         try
         {
            // The game may already hold this exact item's card (an identical re-select can emit no update).
            var current:Object = BSUIDataManager.GetDataFromClient("InventoryItemCardData").data;
            if(current != null && Number(current["serverHandleID"]) == itCardWait)
            {
               itCardAccept(current);
               return;
            }
         }
         catch(e:Error) {}
         itCardTimer = setTimeout(itCardTimeout,1500);
         try
         {
            BSUIDataManager.dispatchEvent(new CustomEvent(EVENT_ITEM_SELECTED,{
               "serverHandleID":row["serverHandleID"],
               "isSelectionValid":true,
               "fromContainer":itSrc != "player"
            }));
         }
         catch(e:Error)
         {
            clearTimeout(itCardTimer);
            itCardWait = -1;
            itCards[itCardKey(row)] = {"status":"select-error"};
            itQueuePos++;
            itCardTimer = setTimeout(itCardNext,30);
         }
      }

      private function itCardTimeout() : void
      {
         if(!itAlive) return;
         if(!itCardRetry)
         {
            itCardRetry = true;
            itCardNext();
            return;
         }
         itCardRetry = false;
         var row:Object = itQueue[itQueuePos];
         itCards[itCardKey(row)] = {"status":"timeout"};
         itCardWait = -1;
         itQueuePos++;
         itCardNext();
      }

      private function itCardData(event:FromClientDataEvent) : void
      {
         if(itCardWait < 0 || !itAlive) return;
         var d:Object = event.data;
         if(d == null || Number(d["serverHandleID"]) != itCardWait) return;
         itCardAccept(d);
      }

      private function itCardAccept(d:Object) : void
      {
         clearTimeout(itCardTimer);
         itCardRetry = false;
         var desc:String = null;
         var mods:Number = -1;
         var src:Array = d["itemCardEntries"] as Array;
         if(src != null)
         {
            var i:int = 0;
            while(i < src.length)
            {
               var e:Object = src[i];
               if(e["text"] == "DESC") desc = String(e["value"]);
               else if(e["text"] == "legendaryMods") mods = Number(e["value"]);
               i++;
            }
         }
         itCards[itCardKey(itQueue[itQueuePos])] = {"status":desc == null ? "no-desc" : "ok","desc":desc,"mods":mods};
         itCardWait = -1;
         itQueuePos++;
         if(itQueuePos % 5 == 0) itStatusMsg("Reading item cards " + itQueuePos + "/" + itQueue.length + "...");
         itCardTimer = setTimeout(itCardNext,60);
      }

      private function itFinishCards() : void
      {
         if(itHasOrig && itQueue.length > 0)
         {
            try
            {
               BSUIDataManager.dispatchEvent(new CustomEvent(EVENT_ITEM_SELECTED,{
                  "serverHandleID":itOrigId,
                  "isSelectionValid":true,
                  "fromContainer":itOrigFrom
               }));
            }
            catch(e:Error) {}
         }
         try { itSave(); }
         catch(e:Error) { itStatusMsg("Export failed: " + e); }
         itBusy = false;
         itRefresh();
      }

      // ---- serialization ----
      private function itQuote(value:String) : String
      {
         var output:String = "\"";
         var i:int = 0;
         while(i < value.length)
         {
            var code:int = value.charCodeAt(i);
            if(code == 34) output += "\\\"";
            else if(code == 92) output += "\\\\";
            else if(code < 32) output += "\\u" + ("0000" + code.toString(16)).substr(-4);
            else output += value.charAt(i);
            i++;
         }
         return output + "\"";
      }

      private function itNum(v:Object) : String
      {
         var n:Number = Number(v);
         if(!isFinite(n)) return "null";
         return String(Math.round(n * 100) / 100);
      }

      private function itBool(v:Object) : String
      {
         return v ? "true" : "false";
      }

      private function itUtf8(s:String) : int
      {
         var n:int = 0;
         var i:int = 0;
         while(i < s.length)
         {
            var c:int = s.charCodeAt(i);
            n += c < 128 ? 1 : (c < 2048 ? 2 : 3);
            i++;
         }
         return n;
      }

      private function itHex(n:Number) : String
      {
         var hi:Number = Math.floor(n / 65536);
         var lo:Number = n - hi * 65536;
         var l:String = int(lo).toString(16);
         while(l.length < 4) l = "0" + l;
         return int(hi).toString(16) + l;
      }

      private function itRowStr(r:Object) : String
      {
         var name:String = r["text"] == null ? "" : String(r["text"]);
         var s:String = "{\"name\":" + itQuote(name) + ",\"count\":" + itNum(r["count"]) + ",\"id\":" + itNum(r["serverHandleID"]) + ",\"value\":" + itNum(r["itemValue"]) + ",\"equipState\":" + itNum(r["equipState"]) + ",\"filter\":" + itNum(r["filterFlag"]) + ",\"level\":" + itNum(r["itemLevel"]) + ",\"health\":" + itNum(r["currentHealth"]) + ",\"maxHealth\":" + itNum(r["maximumHealth"]) + ",\"weight\":" + itNum(r["weight"]) + ",\"legendary\":" + itBool(r["isLegendary"]) + ",\"stars\":" + itNum(r["numLegendaryStars"]) + ",\"favorite\":" + itBool(r["favorite"]) + ",\"quest\":" + itBool(r["isQuestItem"]) + ",\"category\":" + itNum(r["itemCategory"]);
         // CAMP machine assignment: every row has vendingData; machineType 0 means "not assigned". Type 1 is the
         // player's vending machine and then price is the listed price per item (the game's own list shows it).
         // Other types: 2 display case, 3 dispenser, 4 fermenter, 5 refrigerator, 6 ally, 7 recharger, 8 freezer, 9 pet.
         try
         {
            var vd:Object = r["vendingData"];
            if(vd != null && Number(vd["machineType"]) > 0)
            {
               s += ",\"vending\":{\"machine\":" + itNum(vd["machineType"]) + ",\"price\":" + itNum(vd["price"]) + ",\"other\":" + itBool(vd["isVendedOnOtherMachine"]) + "}";
            }
         }
         catch(e:Error) {}
         if(itNeedsCard(r))
         {
            var card:Object = itCards[itCardKey(r)];
            if(card == null) s += ",\"card\":\"missing\"";
            else
            {
               s += ",\"card\":" + itQuote(String(card["status"]));
               if(card["status"] == "ok")
               {
                  if(Number(card["mods"]) >= 0) s += ",\"legendaryMods\":" + itNum(card["mods"]);
                  s += ",\"desc\":" + itQuote(String(card["desc"]));
               }
            }
         }
         return s + "}";
      }

      private function itDeflate(text:String) : flash.utils.ByteArray
      {
         var ba:flash.utils.ByteArray = new flash.utils.ByteArray();
         ba.writeUTFBytes(text);
         ba.compress();
         return ba;
      }

      private function itB64(ba:flash.utils.ByteArray) : String
      {
         var out:Array = [];
         var n:int = ba.length;
         ba.position = 0;
         var i:int = 0;
         while(i < n)
         {
            var b0:int = ba.readUnsignedByte();
            var b1:int = i + 1 < n ? ba.readUnsignedByte() : 0;
            var b2:int = i + 2 < n ? ba.readUnsignedByte() : 0;
            out.push(itB64Chars.charAt(b0 >> 2) + itB64Chars.charAt(((b0 & 3) << 4) | (b1 >> 4)) + (i + 1 < n ? itB64Chars.charAt(((b1 & 15) << 2) | (b2 >> 6)) : "=") + (i + 2 < n ? itB64Chars.charAt(b2 & 63) : "="));
            i += 3;
         }
         return out.join("");
      }

      // Filename-safe identity token: letters and digits only; a short hash keeps distinct raw names distinct.
      private function itToken(raw:String, maxLen:int) : String
      {
         var clean:String = "";
         var h:int = 0;
         var i:int = 0;
         while(i < raw.length)
         {
            var c:int = raw.charCodeAt(i);
            h = (h * 31 + c) % 65536;
            if((c >= 48 && c <= 57) || (c >= 97 && c <= 122)) clean += raw.charAt(i);
            else if(c >= 65 && c <= 90) clean += raw.charAt(i).toLowerCase();
            i++;
         }
         var hex:String = h.toString(16);
         while(hex.length < 4) hex = "0" + hex;
         // Only lowercase letters, digits and underscores have ever been accepted as storage names.
         if(clean == raw.toLowerCase() && clean.length <= maxLen && clean.length > 0) return clean;
         if(clean.length > maxLen - 4) clean = clean.substr(0,maxLen - 4);
         return clean + hex;
      }

      // The character's currencies as the game's own HUD reads them: CharacterInfoData.currencies is a list of
      // {currencyType, currencyAmount, currencyMax} (type 0 caps, 1 legendary tokens, 2 possum badges, 3 tadpole
      // badges, 4 gold bullion, 5 perk coins, 6-9 legendary cores tier 1-4, 10 ultracite battery,
      // 11 expedition stamps, 12 supplies). Raw numbers are exported; the viewer names them.
      private function itCurrenciesJson() : String
      {
         try
         {
            var list:Array = BSUIDataManager.GetDataFromClient("CharacterInfoData").data["currencies"] as Array;
            if(list == null) return "";
            var out:Array = [];
            var i:int = 0;
            while(i < list.length)
            {
               var c:Object = list[i];
               out.push("{\"type\":" + itNum(c["currencyType"]) + ",\"amount\":" + itNum(c["currencyAmount"]) + ",\"max\":" + itNum(c["currencyMax"]) + "}");
               i++;
            }
            return ",\n\"currencies\":[" + out.join(",") + "]";
         }
         catch(e:Error) {}
         return "";
      }

      private function itSave() : void
      {
         var account:String = "";
         var character:String = "";
         try { account = String(BSUIDataManager.GetDataFromClient("AccountInfoData").data["name"]); } catch(e:Error) {}
         try { character = String(BSUIDataManager.GetDataFromClient("CharacterInfoData").data["name"]); } catch(e:Error) {}
         var label:String = itStorageLabel(itSrc);
         var rowStrs:Array = [];
         var hash:Number = 0;
         var M:Number = 4294967296;
         var cardsOk:int = 0;
         var cardsBad:int = 0;
         var i:int = 0;
         while(i < itSorted.length)
         {
            var r:Object = itSorted[i];
            rowStrs.push(itRowStr(r));
            var idm:Number = ((Number(r["serverHandleID"]) % M) + M) % M;
            hash = (hash * 31 + idm + Math.abs(Number(r["count"]))) % M;
            if(itNeedsCard(r))
            {
               var cd:Object = itCards[itCardKey(r)];
               if(cd != null && cd["status"] == "ok") cardsOk++;
               else cardsBad++;
            }
            i++;
         }
         var snap:String = itHex(hash);
         if(rowStrs.length == 0) { itStatusMsg("Nothing to export: the list is empty."); return; }
         // Split into the fewest parts whose compressed payloads fit the 40 KiB storage limit.
         var all:String = "[\n" + rowStrs.join(",\n") + "\n]";
         var deflated:int = itDeflate(all).length;
         var k:int = Math.max(1,Math.ceil((deflated * 4 / 3) / 36500));
         var parts:Array = null;
         while(parts == null && k <= 60)
         {
            var per:int = Math.ceil(rowStrs.length / k);
            var chunks:Array = [];
            var fits:Boolean = true;
            var c:int = 0;
            while(c < rowStrs.length)
            {
               var rows:Array = rowStrs.slice(c,c + per);
               var est:int = 800 + Math.ceil(itDeflate("[\n" + rows.join(",\n") + "\n]").length / 3) * 4;
               if(est > 40000) { fits = false; break; }
               chunks.push(rows);
               c += per;
            }
            if(fits) parts = chunks;
            else k++;
         }
         if(parts == null) { itStatusMsg("Export too large to split."); return; }
         if(itPart > parts.length)
         {
            itStatusMsg(label + " needs " + parts.length + " part(s); part " + itPart + " does not exist. Pick a lower part.");
            return;
         }
         var chosen:Array = parts[itPart - 1];
         var text:String = "[\n" + chosen.join(",\n") + "\n]";
         var header:String = "{\n\"v\":1,\n\"tool\":\"InventoryTracking\",\n\"account\":" + itQuote(account) + ",\n\"character\":" + itQuote(character) + ",\n\"storage\":\"" + label + "\",\n\"storageMode\":" + itMode + ",\n\"title\":" + itQuote(itSrc == "player" ? "Player inventory" : itTitle) + (itSrc == "player" ? itCurrenciesJson() : "") + ",\n\"snapshot\":\"" + snap + "\",\n\"rows\":" + rowStrs.length + ",\n\"part\":" + itPart + ",\n\"parts\":" + parts.length + ",\n\"rowsInPart\":" + chosen.length + ",\n\"cards\":\"legendary+mods\",\n\"cardsOk\":" + cardsOk + ",\n\"cardsMissing\":" + cardsBad + ",\n\"encoding\":\"zlib+base64\",\n\"rawBytes\":" + itUtf8(text) + ",\n\"data\":\"";
         var payload:String = header + itB64(itDeflate(text)) + "\"\n}\n";
         var bytes:int = itUtf8(payload);
         if(bytes > 40960) { itStatusMsg("Part is over the 40 KiB limit (" + bytes + " bytes); not saved."); return; }
         var bridge:Object = __SFCodeObj;
         if(bridge == null || bridge.modStorage == null) { itStatusMsg("xScal storage is not available."); return; }
         var name:String = "itemdump_" + itToken(account,14) + "_" + itToken(character,14) + "_" + label + "_" + itPart;
         var storage:Object = bridge.modStorage;
         if(storage.register(name) !== true)
         {
            itStatusMsg("Storage refused the name " + name + " (" + name.length + " chars). Close and reopen the menu and try once more; if it repeats, report this text.");
            return;
         }
         var saved:Object = storage.save(payload);
         var verified:Boolean = false;
         try { verified = storage.load() === payload; } catch(e:Error) {}
         if(saved === true && verified)
         {
            itDone = true;
            itStatusMsg("Exported " + itDisplayName(itSrc) + " Part " + itPart + "/" + parts.length + " - please reopen the box for other exports");
         }
         else itStatusMsg("Save failed for " + name + " (save=" + saved + ", verified=" + verified + ").");
      }

      private function itClose(event:flash.events.Event) : void
      {
         if(event.target != this) return;
         itGuardOff();
         itAlive = false;
         clearTimeout(itPollTimer);
         clearTimeout(itCardTimer);
         var list:Array = [itBtnPlayer,itBtnBox,itBtnPart,itBtnVer,itStatus];
         var i:int = 0;
         while(i < list.length)
         {
            var b:flash.text.TextField = list[i];
            if(b != null && b.parent != null) b.parent.removeChild(b);
            i++;
         }
         itBtnPlayer = null;
         itBtnBox = null;
         itBtnPart = null;
         itBtnVer = null;
         itStatus = null;
         removeEventListener(flash.events.Event.REMOVED_FROM_STAGE,itClose);
      }
