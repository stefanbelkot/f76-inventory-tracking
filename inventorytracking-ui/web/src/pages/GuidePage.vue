<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import DownloadsTable from '../components/DownloadsTable.vue'
import { GUIDE_FACTS as facts, GUIDE_SECTIONS, parseGuideHash, sectionAnchor } from '../utils/guideFacts'

const contentsOpen = ref(false)

function goTo(id: string): void {
  contentsOpen.value = false
  document.getElementById(sectionAnchor(id))?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  history.replaceState(null, '', `#${sectionAnchor(id)}`)
}

onMounted(async () => {
  const { section } = parseGuideHash(window.location.hash)
  if (section) {
    await nextTick()
    document.getElementById(sectionAnchor(section))?.scrollIntoView({ block: 'start' })
  }
})
</script>

<template>
  <main class="guide">
    <aside class="contents" aria-label="Contents">
      <button type="button" class="btn contents-toggle" :aria-expanded="contentsOpen" @click="contentsOpen = !contentsOpen">
        {{ contentsOpen ? '▾' : '▸' }} Contents
      </button>
      <nav :class="{ open: contentsOpen }">
        <a v-for="section in GUIDE_SECTIONS" :key="section.id" :href="`#${sectionAnchor(section.id)}`" @click.prevent="goTo(section.id)">
          {{ section.title }}
        </a>
      </nav>
    </aside>

    <article class="text">
      <h2>Guide</h2>

      <section :id="sectionAnchor('overview')">
        <h3>What this is</h3>
        <p>
          <strong>InventoryTracking</strong> is a Fallout 76 interface mod plus a viewer. The mod adds export buttons to your
          storage menus; the viewer shows what you exported: every character and storage, filters, search, vendor listings and a
          calculator for legendary mods.
        </p>
        <ol class="flow">
          <li><strong>Mod</strong> in the game: you press an export button in a storage menu.</li>
          <li><strong>Files</strong> appear in the game's <code>Data\modsdata</code> folder (<code>itemdump_*.json</code>).</li>
          <li><strong>Viewer</strong> on your PC reads them, or the <strong>Syncer</strong> uploads them automatically.</li>
          <li><strong>This site</strong> shows them, on any device, with a link only you know.</li>
        </ol>
        <p class="muted">The mod only reads. It never moves, scraps, renames or changes anything in your inventory.</p>
      </section>

      <section :id="sectionAnchor('requirements')">
        <h3>What you need</h3>
        <ul>
          <li>Fallout 76 on PC: Steam, or Microsoft Store / Xbox Game Pass.</li>
          <li>
            The <strong>xScal Scaleform Extender</strong>. The mod saves its files through it (interface mods cannot write files on
            their own). Install it first and make sure it works.
          </li>
          <li>Optional: <strong>InventOmaticStash-UO</strong> (see the next section).</li>
        </ul>
        <div class="callout">
          <strong>Tested with:</strong> game runtime {{ facts.game }}, <strong>xScal {{ facts.xscal }}</strong> (Xbox / Game Pass; the xScal
          author reports no difference on Steam), InventOmaticStash-UO <strong>{{ facts.inventomatic }}</strong>.<br />
          <strong>Not tested with ZFE.</strong> ZFE offers the same storage interface, so it should work too, but nobody has checked.
          If you try it, please report the result.
        </div>
      </section>

      <section :id="sectionAnchor('versions')">
        <h3>Which mod version</h3>
        <table class="plain">
          <thead>
            <tr><th>Version</th><th>Use it when</th><th>What it does</th></tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Standalone</strong></td>
              <td>You do not use InventOmaticStash-UO.</td>
              <td>Adds the export buttons to the normal game menu.</td>
            </tr>
            <tr>
              <td><strong>Compatible with InventOmaticStash-UO</strong></td>
              <td>You use InventOmaticStash-UO.</td>
              <td>
                The same buttons together with InventOmaticStash-UO's menu, so both mods work side by side.
                <strong>Guaranteed with InventOmaticStash-UO {{ facts.inventomatic }}.</strong>
              </td>
            </tr>
          </tbody>
        </table>
        <ul>
          <li>Use <strong>one</strong> of the two, never both. Keep <code>InventOmaticStash-UO.ba2</code> enabled when you use the compatible version.</li>
          <li>
            Other mods that replace the storage menu cannot be combined with either version; the game loads only one of them.
          </li>
          <li>
            If InventOmaticStash-UO is updated, the compatible version may hide the update's menu changes until a new compatible
            version is published here. Check the version in the downloads table.
          </li>
          <li>The InventOmaticStash-UO author has not been contacted yet; the compatible version is our own build made for {{ facts.inventomatic }}.</li>
        </ul>
      </section>

      <section :id="sectionAnchor('install')">
        <h3>Install the mod</h3>
        <h4>By hand (works for everybody)</h4>
        <ol>
          <li>Download the zip for your version (see Downloads) and unzip it.</li>
          <li>
            Copy <code>InventoryTracking.ba2</code> into the game's <code>Data</code> folder:<br />
            Steam: <code>...\steamapps\common\Fallout76\Data</code><br />
            Xbox / Game Pass: <code>&lt;drive&gt;:\XboxGames\Fallout 76\Content\Data</code>
          </li>
          <li>
            Open (or create) the custom INI in <code>Documents\My Games\Fallout 76</code>:
            <code>Fallout76Custom.ini</code> on Steam, <code>Project76Custom.ini</code> on Xbox / Game Pass.
          </li>
          <li>
            Under <code>[Archive]</code> add <code>InventoryTracking.ba2</code> to <code>sResourceArchive2List</code>,
            <strong>as the last entry</strong>, separated by commas without spaces:
            <pre>[Archive]
sResourceArchive2List=SomeOther.ba2,InventOmaticStash-UO.ba2,InventoryTracking.ba2</pre>
          </li>
          <li>
            Start the game and open your stash. The label <strong><code>{{ facts.modLabel }}</code></strong> at the top left of the
            menu shows that the mod is loaded.
          </li>
        </ol>
        <p class="muted">Switching between the two versions: replace the same file, never list it twice.</p>
        <h4>With a mod manager</h4>
        <p>
          The zips are laid out for managers (the archive sits at the zip root, <code>INSTALL.txt</code> beside it). Good choices:
        </p>
        <ul>
          <li>
            <strong>Nukamods</strong> – made for Fallout 76, supports Steam and Xbox / Game Pass, copies the archives into
            <code>Data</code> and lets you just enable or disable mods. It is in beta, so back up your INI first.
          </li>
          <li>
            <strong>Fallout 76 Quick Configuration</strong> – an INI editor and mod manager that detects Steam and Xbox installs,
            takes drag-and-drop <code>.ba2</code> mods, enables or disables them in bulk and shows a live log when a mod does not load.
          </li>
        </ul>
        <p>
          With any manager, check once that <code>InventoryTracking.ba2</code> is <strong>last</strong> in the archive list and that
          <code>{{ facts.modLabel }}</code> shows in game. Mod managers are community tools; the manual way above always works.
        </p>
      </section>

      <section :id="sectionAnchor('ingame')">
        <h3>Use it in the game</h3>
        <p>
          Open a storage menu: your <strong>stash</strong>, a <strong>scrap box</strong>, an <strong>ammo box</strong> or an
          <strong>aid box</strong>. The buttons appear at the top left (not at workbenches or workshops, not when looting a corpse).
        </p>
        <table class="plain">
          <thead>
            <tr><th>Button</th><th>Where</th><th>What it exports</th></tr>
          </thead>
          <tbody>
            <tr><td>Export player inventory</td><td>stash menu only</td><td>Everything you carry, plus your currencies</td></tr>
            <tr><td>Export stash / scrap box / ammo box / aid box</td><td>each of those menus</td><td>The box you opened</td></tr>
            <tr><td>Part N</td><td>all</td><td>Only for very big boxes: if one file is too small, pick the next part and export again. Normally leave it at 1.</td></tr>
            <tr><td><code>{{ facts.modLabel }}</code></td><td>all</td><td>Just the version label</td></tr>
          </tbody>
        </table>
        <ul>
          <li>
            <strong>One export per menu open.</strong> After an export the buttons say "close and reopen the menu": close the box and
            open it again for the next export. This is a limit of the extender, not a fault.
          </li>
          <li>Big boxes take a few seconds: legendary items and legendary modules are read one by one. Wait for the success message.</li>
          <li>
            <strong>Several characters and accounts:</strong> every file name contains the account and the character
            (<code>itemdump_&lt;account&gt;_&lt;character&gt;_&lt;storage&gt;_1.json</code>), so each character gets its own files and
            nothing is overwritten by another one. Exporting the same box again replaces only that file; there is no history.
          </li>
          <li>
            A good routine per character: open the stash, export the player inventory and the stash, close, reopen, then do the scrap,
            ammo and aid boxes. The viewer sorts everything by account and character.
          </li>
        </ul>
        <p class="muted">Files are written to <code>Data\modsdata</code> inside the game folder.</p>
      </section>

      <section :id="sectionAnchor('data')">
        <h3>Get the data into the viewer</h3>
        <table class="plain">
          <thead>
            <tr><th>Way</th><th>Good for</th><th>How</th></tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Local viewer</strong> (<code>InventoryTrackingViewer.exe</code>)</td>
              <td>Looking at your own data on your PC</td>
              <td>It finds the Steam and Xbox folders itself and refreshes automatically.</td>
            </tr>
            <tr>
              <td><strong>This site + Syncer</strong></td>
              <td>Viewing from any device, sharing a link</td>
              <td>
                Open the <em>Import</em> tab: it creates a session and shows your <strong>session link</strong>. Start the Syncer, paste
                the link once. It uploads at start and after every export.
              </td>
            </tr>
            <tr>
              <td><strong>Manual upload</strong></td>
              <td>A one-off look</td>
              <td><em>Import</em> tab, choose the <code>itemdump_*.json</code> files.</td>
            </tr>
          </tbody>
        </table>
        <h4>The Syncer in short</h4>
        <ul>
          <li>On the first start it asks whether to start with Windows, finds your Steam and Xbox export folders and asks for the session link.</li>
          <li>Tray icon menu: Sync now, Open viewer, Update session, Update Xbox / Steam folders, Start with Windows, Quit.</li>
          <li>It uploads changed files only. A file you delete from the game folder is removed from your session too.</li>
          <li>It never changes your game files. Settings and log: <code>%LOCALAPPDATA%\InventoryTrackingSyncer</code>.</li>
        </ul>
        <div class="callout">
          <strong>Privacy:</strong> uploading sends your export files, including account and character names, to this site. Your
          <strong>session link is the only key</strong>: anybody who has it can see that data. Share it only on purpose. A session is
          kept for a week after its last use.
        </div>
        <div class="callout">
          <strong>"Windows protected your PC"?</strong> The Syncer is a free hobby project and not code-signed yet, so Windows
          SmartScreen may call it an "unrecognized app" from an "Unknown publisher". That is a reputation check, not a virus
          finding. Click <strong>More info</strong>, then <strong>Run anyway</strong>. To avoid the dialog, right-click the downloaded
          zip, choose Properties, tick <strong>Unblock</strong>, then extract it. You can compare the SHA-256 in the downloads
          table with the file you got.
        </div>
      </section>

      <section :id="sectionAnchor('viewer')">
        <h3>Using the viewer</h3>
        <ul>
          <li><strong>Inventory:</strong> pick everything, one character (click its name) or one storage; sort, filter by type, stars, legendary, vendor listings; search names and effects.</li>
          <li><strong>Details:</strong> click an item for all fields and legendary effects by star; search links open Google or the wiki.</li>
          <li><strong>Vendor:</strong> items listed at your CAMP vending machine show "FOR SALE" and their price.</li>
          <li><strong>Mod calculator:</strong> turns legendary mods into legendary modules and Leaders per star tier. Mark valuable mods and the rest counts as junk.</li>
          <li><strong>Copy and download:</strong> the filtered list can be copied or downloaded as CSV or JSON.</li>
          <li><strong>Phone:</strong> on a narrow screen the list becomes cards and the filters fold into one bar.</li>
        </ul>
      </section>

      <section :id="sectionAnchor('help')">
        <h3>Troubleshooting</h3>
        <dl class="faq">
          <dt>No buttons in the storage menu</dt>
          <dd>
            The archive is not in <code>Data</code>, not <strong>last</strong> in <code>sResourceArchive2List</code>, xScal is not installed
            (look at <code>xscal.log</code> in <code>Data</code>), or another mod replaces the storage menu. Look for
            <code>{{ facts.modLabel }}</code> at the top left.
          </dd>
          <dt>The buttons show but saving fails</dt>
          <dd>xScal missing or a different version, or you already exported once in this menu (close and reopen), or the box is huge (use Part N).</dd>
          <dt>Nothing shows up in the viewer</dt>
          <dd>The export was not done yet, the viewer or Syncer watches the wrong folder, or the session expired. Check the folder list and create a new session if needed.</dd>
          <dt>Syncer says "Session unknown or expired"</dt>
          <dd>Open the <em>Import</em> tab for a new link, then use the tray menu: Update session.</dd>
          <dt>Buttons vanished after a game or InventOmaticStash-UO update</dt>
          <dd>The archive may be outdated. Check the Downloads table for a newer version.</dd>
          <dt>Something looks wrong after an export</dt>
          <dd>Close the menu with the normal key and reopen it. Note your game version and report it.</dd>
        </dl>
        <p class="muted">
          Source code, license (MIT) and issue tracker:
          <a href="https://github.com/stefanbelkot/f76-inventory-tracking" target="_blank" rel="noopener">github.com/stefanbelkot/f76-inventory-tracking</a>.
          Please report problems there and include your game version, mod version and what you did.
        </p>
      </section>

      <section :id="sectionAnchor('downloads')">
        <h3>Downloads</h3>
        <p class="muted">
          Pick the mod version that matches your setup (see "Which mod version"). The SHA-256 lets you verify a file.
        </p>
        <DownloadsTable />
      </section>

      <section :id="sectionAnchor('privacy')">
        <h3>About, contact and privacy</h3>
        <p>
          This is a free, non-commercial hobby project by {{ facts.owner }}. It is not affiliated with or endorsed by Bethesda Game
          Studios or ZeniMax Media; Fallout 76 is their trademark. The source code is open (MIT license).
        </p>
        <h4>Contact</h4>
        <p>
          Questions, bug reports and requests (including requests about your data, see below):
          <a :href="`${facts.repoUrl}/issues`" target="_blank" rel="noopener">open an issue on GitHub</a>
          or find {{ facts.owner }} through the
          <a :href="facts.repoUrl" target="_blank" rel="noopener">repository page</a>.
          <template v-if="facts.email">E-mail: <a :href="`mailto:${facts.email}`">{{ facts.email }}</a>.</template>
          Issues are public, so do not post your session link there.
        </p>
        <h4>Privacy</h4>
        <p>The person responsible for this site is {{ facts.owner }} (contact above). What happens with your data:</p>
        <ul>
          <li>
            <strong>Uploads (hosted site only):</strong> when you use the Syncer or the Import tab, the export files you upload are stored on
            the server. They contain item lists and the <strong>account and character names</strong> of your game profile. They are used only to show you
            your own data in the viewer.
          </li>
          <li>
            <strong>Session link:</strong> a random id is the only key to an upload session; there are no user accounts, passwords or tracking.
            Anyone who has your link can see that session's data, so share it only on purpose.
          </li>
          <li>
            <strong>Retention:</strong> a session and its files are deleted automatically one week after its last use. You can stop earlier by
            asking for deletion (see below).
          </li>
          <li>
            <strong>Browser storage:</strong> the page keeps your session id and display settings (filters, calculator, last view) in your browser's
            local storage so that it works when you come back. There are no cookies, no analytics and no advertising.
          </li>
          <li>
            <strong>Server logs:</strong> the web server records normal access logs (time, requested address, IP address, browser type) for
            security and operation. They are not combined with uploaded data and are not shared.
          </li>
          <li>
            <strong>Sharing:</strong> data is not sold or passed on. It is stored on the server that hosts this site, which is run by its owner.
          </li>
          <li>
            <strong>Local viewer and Syncer settings:</strong> the desktop viewer and the Syncer keep their data on your own computer (the Syncer
            only talks to the site address you configured).
          </li>
        </ul>
        <p>
          <strong>Your rights:</strong> under the GDPR you can ask for access to, correction or deletion of the data stored about you, and you
          can complain to the data protection authority (in Sweden: IMY, Integritetsskyddsmyndigheten). To have a session deleted early, contact
          {{ facts.owner }} and name the session id (the part of your session link after <code>session=</code>) or the account name.
        </p>
      </section>
    </article>
  </main>
</template>

<style scoped>
.guide {
  display: grid;
  grid-template-columns: 220px minmax(0, 760px);
  gap: 24px;
  justify-content: center;
  padding: 16px;
}

.contents {
  position: sticky;
  top: 8px;
  align-self: start;
}

.contents-toggle {
  display: none;
}

.contents nav {
  display: grid;
  gap: 2px;
}

.contents nav a {
  padding: 5px 8px;
  border-radius: 6px;
  color: inherit;
  text-decoration: none;
}

.contents nav a:hover {
  background: var(--surface-alt);
}

.text {
  display: grid;
  gap: 18px;
  min-width: 0;
}

section {
  display: grid;
  gap: 8px;
  scroll-margin-top: 8px;
}

h2 {
  font-size: 22px;
}

h3 {
  font-size: 17px;
  padding-bottom: 4px;
  border-bottom: 1px solid var(--border);
}

h4 {
  margin-top: 6px;
  font-size: 14px;
}

p,
ul,
ol {
  margin: 0;
}

ul,
ol {
  padding-left: 22px;
  display: grid;
  gap: 4px;
}

pre {
  margin: 6px 0 0;
  padding: 8px 10px;
  overflow-x: auto;
  background: var(--surface-alt);
  border: 1px solid var(--border);
  border-radius: 6px;
}

code {
  font-family: ui-monospace, Consolas, monospace;
  font-size: 12.5px;
  word-break: break-word;
}

.flow {
  counter-reset: step;
  list-style: none;
  padding: 0;
}

.flow li {
  counter-increment: step;
  padding: 6px 10px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
}

.flow li::before {
  content: counter(step) '. ';
  font-weight: 700;
  color: var(--accent);
}

.callout {
  padding: 10px 12px;
  background: var(--accent-soft);
  border: 1px solid var(--border);
  border-radius: 8px;
}

.plain {
  width: 100%;
  border-collapse: collapse;
}

.plain th,
.plain td {
  padding: 6px 8px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: top;
}

.faq {
  display: grid;
  gap: 4px;
  margin: 0;
}

.faq dt {
  margin-top: 8px;
  font-weight: 600;
}

.faq dd {
  margin: 0;
}

@media (max-width: 700px) {
  .guide {
    grid-template-columns: minmax(0, 1fr);
    gap: 8px;
    padding: 10px;
  }

  .contents {
    position: static;
  }

  .contents-toggle {
    display: block;
    width: 100%;
    text-align: left;
  }

  .contents nav {
    display: none;
    margin-top: 4px;
  }

  .contents nav.open {
    display: grid;
  }

  .plain {
    display: block;
    overflow-x: auto;
  }
}
</style>
