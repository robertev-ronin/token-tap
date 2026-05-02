# GitHub Images

Token-Tap includes repository artwork in source control:

- Logo: [docs/assets/token-tap-logo.png](assets/token-tap-logo.png)
- Performance Monitor screenshot: [docs/assets/perfmon.png](assets/perfmon.png)
- Social preview candidate: [.github/social-preview.png](../.github/social-preview.png)

## Repository Social Preview

GitHub supports a repository Social preview image. It appears when the repository is shared on social platforms.

Use:

```text
.github/social-preview.png
```

Upload path in GitHub:

```text
Repository -> Settings -> Social preview -> Edit -> Upload an image
```

GitHub recommends PNG, JPG, or GIF under 1 MB, with 1280x640 as the best display size. The checked-in `social-preview.png` is under 1 MB and ready to upload.

At the time of this release, GitHub documents Social preview as a repository settings upload. The normal public REST repository update API does not expose a stable first-class field for uploading that image, so the checked-in file is the reliable source asset and the final upload is done in the GitHub Settings UI.
