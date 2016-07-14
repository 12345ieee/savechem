//
// SaveChem : a SpaceChem savegame tool
//
// Version 0.0.2 (alpha)
//

// ---------------------------------------------------------------------------------------------
// Requirements
// ---------------------------------------------------------------------------------------------

- Windows, in all likelihood (I'm not sure how well it'd work using Mono)
- .NET 4.0
- System.Data.SQLite (included)
- NewtonSoft.Json (included)

// ---------------------------------------------------------------------------------------------
// Usage
// ---------------------------------------------------------------------------------------------

This is a small tool that can view the data inside a SpaceChem savegame, and poke around in it 
a little. For now its capabilities are limited, but I think it may prove useful nonetheless.

Features:

- Open savegame:
  Open a savegame by choosing 'Open' from the File menu. 
  The savegames can be found here:
  - Windows XP: C:\Documents and Settings\<username>\Local Settings\Application Data\Zachtronics Industries\SpaceChem\save
  - Windows 7 / Vista: C:\Users\<username>\AppData\Local\Zachtronics Industries\SpaceChem\save
  - OSX / Linux: ~/.local/share/Zachtronics Industries/SpaceChem


- Clear Undo:
  The savegames keep an infinite undo history, meaning that if you've been playing for a while, 
  the savegame is 90% undo information. This allows you do remove that. Select one or more levels
  and hit 'Clear Undo' to remove undo tables for those levels.
  

- Export (to clipboard):
  Select a level and click 'Export to clipboard' to export a solution description to the clipboard.
  Yeah, it's only clipboard for the moment, but that should suffice for an alpha.
  

- Import Level
  Import a custom level. Should be self-explanatory. I will say, however, that the validator won't 
  accept everything yet. If you try to import something that you know is a valid level, but isn't 
  accepted, send it to me and I'll see what I can do.
  At present, it doesn't accept sandbox missions yet.


- Import Solution:
  This is the big one. The 'Import' button opens up the import dialog. Importing a solution 
  consists of 3 steps:
  1) Enter a solution in the 'input' field.
  2) Hit 'search' to find the solution in the savegame (yeah, I can probably make this automatic).
    If a solution exists, you'll be presented with some solution stats on the left, and a list of 
    candidate solutions on the right. 
  3) Select the level to import into. This may get a little tricky. It's really only relevant for 
    custom levels, but here goes. 
  
    To find the level the solution belongs to, the tool searches for 3 things:
    - internal level ID (i)
    - level definition (d)
    - level name (n)
    Levels matching these criteria are the 'candidates', and you need to select one to actually 
    import into. For official levels, there should only be one option, but for custom there 
    may be more. 
    For custom levels, there's also the option to make a copy and import there, rather than just 
    import over an existing solution. You can even provide a new name for the mission. I expect 
    this may be useful for tournaments.

    If all goes well, you can hit OK and the solution will be imported. And if *everything* 
    goes well, the imported solution will actually be playable. I have had some trouble with 
    this, that I think I've sorted, but I can't make any guarantees.


- Slice
  With this feature, you can create a 'slice' of a savegame. Basically, you can create a new 
  .user file with just the selected levels. This is *very* useful when uploading to spacechem.net, 
  and can serve as a sort of multi-export.


- Debug status bar:
  Yeah, this is mostly for my own purposes, but I don't want to remove it yet. Keep moving people, 
  there's nothing to see here.

// ---------------------------------------------------------------------------------------------
// Wishlist
// ---------------------------------------------------------------------------------------------

- (20140810) : View solution.
- (20140810) : Export/import to files.
- (20140810) : Delete solutions, maybe?
- (20140810) : Spruce up interface.
- (20140810) : Create a proper help file/page.

NOTE : this will NOT allow you to run solutions. that's what the game is for, gorrammit.


// ---------------------------------------------------------------------------------------------
// Release notes
// ---------------------------------------------------------------------------------------------

// --- 0.0.2 (20140821) ---
+ Added custom level import.
+ Added Slice capability.
  Note that both features could use a little more testing.

// --- 0.0.1 (20140810) ---
Initial release. I *think* the things I want most are in here now:
+ remove undo info. Per level even! :D
+ export a solution (to clipboard, but it's something)
+ import a solution, with creating an optional copy


// ---------------------------------------------------------------------------------------------
// Disclaimer
// ---------------------------------------------------------------------------------------------

I make no guarantees on whether or not this will screw up the savegame. I've tested Clear Undo 
and Import and they *should* work, but I can't be held responsible if the savegame becomes 
corrupt. At present, I'd still suggest creating a backup before you start importing things.


// ---------------------------------------------------------------------------------------------
// Contact
// ---------------------------------------------------------------------------------------------

If you have questions, comments, suggestions, this is where you can find me. If the tool doesn't 
work, or if it messed up the savegame in a way you didn't ask for, please contact me.

I'd also like to know just how big some people's UndoCount has become. My record is 8000.

Steam : cearn
Email : cearn@coranac.com
Website : http:coranac.com

// ---------------------------------------------------------------------------------------------
// References
// ---------------------------------------------------------------------------------------------

* SpaceChem (http://www.zachtronics.com/spacechem/) 
  The game itself. You should already have this >_>
  
* Json.NET (http://james.newtonking.com/json)
  For reading/writing json things.
  
* System.Data.SQLite (http://system.data.sqlite.org/index.html/doc/trunk/www/index.wiki)
  For interfacing with the savegame. For anyone else trying this : GET THE 32-BIT INSTALLER!
  The 64-bit versions require InterOps DLLs, which are nowhere to be found.
  