# Automatic builds: one-time setup

Every push to GitHub runs three things automatically (see `.github/workflows/ci.yml`):

1. **Simulation tests (.NET)**: works straight away, no setup needed.
2. **Unity tests**: runs the edit-mode and play-mode tests inside a real Unity editor.
3. **Windows build**: makes a playable `.exe` you can download from the run's page.

Jobs 2 and 3 use [GameCI](https://game.ci) (free) and need your free Unity Personal licence.
Until you add it they are skipped, not failed. It takes about 10 minutes, once.

## Step 1: activate Unity Personal on your computer

1. Install [Unity Hub](https://unity.com/download) and sign in with your Unity account.
2. In Unity Hub open **Settings** (gear icon) > **Licenses** > **Add** > **Get a free personal license**.

## Step 2: find the licence file

It is a text file called `Unity_lic.ulf`:

| Computer | Location |
| --- | --- |
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` (ProgramData is hidden: paste the path into File Explorer's address bar) |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |

Open it in Notepad (or TextEdit) and copy **everything** in it.

## Step 3: add three secrets to GitHub

On GitHub open the repository, then **Settings** > **Secrets and variables** > **Actions** >
**New repository secret**. Add these three, one at a time:

| Name | Value |
| --- | --- |
| `UNITY_LICENSE` | the whole contents of `Unity_lic.ulf` |
| `UNITY_EMAIL` | the email you use to sign in to Unity |
| `UNITY_PASSWORD` | your Unity password |

Secrets are encrypted. Nobody can read them back, not even you, and they never appear in logs.

**If you sign in to Unity with Google or Apple**, you do not have a Unity password yet: set one at
<https://id.unity.com> first. If you have two-factor sign-in turned on for your Unity account,
the automated activation may fail; GameCI's docs list the current workarounds.

## Step 4: run it

Open the **Actions** tab, choose **CI**, then **Run workflow**. The first Unity run takes about
20 to 30 minutes because it downloads the editor and imports the project; later runs are faster
thanks to caching. When the Windows build finishes, the run page has a **FrogTerrarium-Windows**
download.

## Cost

The repository is public, so GitHub Actions minutes are free and unlimited. If you ever make the
repository private, you get about 2,000 free minutes a month, which is roughly 60 Unity runs.

## Never commit the licence file

`.gitignore` already blocks `*.ulf`, but keep the file out of the project folder anyway.
