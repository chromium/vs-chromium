When making a new release

Master branch
=============
1. Update version # in all files:

  * "Find In Files": "0.9.xx", "Entire Solution"

    * src\Core\VsChromiumVersion.cs
    * src\VsChromium\source.extension.vsixmanifest (PackageManifest\Metadata\Identity\@Version)
    * src\Native\version.rc
  * "Find In Files": "0,9,xx", "Entire Solution"

    * src\Native\version.rc

2. Build

3. Check all unit test pass

4. Commit and push to github

5. Create a release named "vx.y.x" (e.g. v0.2.2) on github

   * Create the tag "vx.y.z"
   * Attach the vsix file built in step 2.


gh-pages branch
===============

1. Update the "What's new" section in "index.html"

2. Update the file "latest_version.txt" with the new version # and new URL

    version: x.y.z

    url: https://github.com/chromium/vs-chromium/releases/tag/vx.y.z

3. Commit and push.
