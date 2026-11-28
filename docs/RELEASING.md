# Releasing a build for players

Releases are made by `.github/workflows/release.yml`. Each release gives a zip on a GitHub
Release page (a download link that does not expire, unlike the CI build links) and, once it is
set up, an upload to itch.io.

## Make a release

1. Merge everything you want into `main` and check CI is green.
2. Pick a version number: `MAJOR.MINOR.PATCH`, for example `0.5.0`. Bump the middle number for new
   features and the last number for fixes.
3. On GitHub: **Actions** > **Release** > **Run workflow**, type the version (without a `v`), and
   run it. (Or, from a terminal: `git tag v0.5.0 && git push origin v0.5.0`.)
4. After about 15 minutes the zip is on the repository's **Releases** page, and on itch.io if
   that is set up.

The game shows its version at the bottom of the Settings window, and crash reports include it.

## One-time itch.io setup (free)

itch.io is free to publish on and lets you choose what share (if any) it takes if you sell later.

1. Make an account at <https://itch.io> and choose **Upload new project**.
   * Kind of project: **Downloadable**. Pricing: **No payments** (or "$0 or donate").
   * Leave it as **Draft** (only you can see it) until you are happy, then switch to **Public**.
   * Use `docs/ITCH_PAGE.md` for the title, description, tags and screenshots list.
2. Note the page address, for example `https://yourname.itch.io/frog-terrarium`. The upload target
   is `yourname/frog-terrarium`.
3. Get an API key: itch.io **Settings** > **API keys** > **Generate new API key**. Copy it.
4. In this GitHub repository: **Settings** > **Secrets and variables** > **Actions**:
   * **Secrets** tab > **New repository secret**: name `BUTLER_API_KEY`, value the key.
   * **Variables** tab > **New repository variable**: name `ITCH_TARGET`, value `yourname/frog-terrarium`.
5. The next release uploads to itch.io's `windows` channel by itself. On the itch.io page, edit
   the upload once to tick **Windows** as its platform.

The API key can publish to your itch.io account, so keep it only in the GitHub secret.
