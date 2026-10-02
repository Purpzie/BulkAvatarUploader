# Bulk Avatar Uploader
[![Add to VCC](https://img.shields.io/badge/Add_to-VCC-blue)](vcc://vpm/addRepo?url=https%3A%2F%2Fpurpzie.com/vpm)
[![Release version](https://img.shields.io/github/v/release/Purpzie/BulkAvatarUploader)](https://github.com/Purpzie/BulkAvatarUploader/releases)
![No AI](https://img.shields.io/badge/No_AI-green.svg?logo=data:image/svg%2bxml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNzYiIGhlaWdodD0iMjc2Ij48Y2lyY2xlIGN4PSIxMzgiIGN5PSIxMzgiIHI9IjEyMCIgZmlsbD0iIzAwMCIvPjxjaXJjbGUgY3g9IjEzOCIgY3k9IjEzOCIgcj0iMTI0IiBzdHJva2U9IiNCMzAwMDAiIHN0cm9rZS13aWR0aD0iMjgiIGZpbGw9Im5vbmUiLz48cmVjdCB4PSI4MCIgeT0iMTQiIHdpZHRoPSIzMiIgaGVpZ2h0PSIyNDIiIGZpbGw9IiNGRkYiIHRyYW5zZm9ybT0ic2tld1goLTkpIi8+PHJlY3QgeD0iOTIiIHk9IjE0IiB3aWR0aD0iMzIiIGhlaWdodD0iMjQyIiBmaWxsPSIjRkZGIiB0cmFuc2Zvcm09InNrZXdYKDkpIi8+PHJlY3QgeD0iNzgiIHk9IjE3MyIgd2lkdGg9IjUwIiBoZWlnaHQ9IjMyIiBmaWxsPSIjRkZGIi8+PHJlY3QgeD0iMTgwIiB5PSIxNSIgd2lkdGg9IjM2IiBoZWlnaHQ9IjIzMCIgZmlsbD0iI0ZGRiIvPjxjaXJjbGUgY3g9IjEzOCIgY3k9IjEzOCIgcj0iMTI0IiBzdHJva2U9IiNCMzAwMDAiIHN0cm9rZS13aWR0aD0iMjgiIGZpbGw9Im5vbmUiIHN0cm9rZS1kYXNoYXJyYXk9IjM5MCIvPjxsaW5lIHgxPSI0NSIgeTE9IjQ1IiB4Mj0iMjMxIiB5Mj0iMjMxIiBzdHJva2U9IiNCMzAwMDAiIHN0cm9rZS13aWR0aD0iMjIiLz48L3N2Zz4=)

One of the best ways to improve performance rank is to [split avatars into separate versions](https://docs.unity3d.com/2022.3/Documentation/Manual/PrefabVariants.html), but they're extremely annoying to upload one by one. This script can take care of it for you under `Tools -> Bulk Avatar Uploader`.

<p float="left">
<img width="49%" src="https://github.com/user-attachments/assets/1caef930-509f-4c77-92fe-be7bdec421fa" />
<img width="49%" src="https://github.com/user-attachments/assets/4d2db2e8-549f-484c-bf49-c4cdda7f1536" />
</p>

You can pause, resume, or cancel the process. If an avatar fails, it pauses to show you why. Status colors can be changed in settings.

Inspired by [I5UCC's VRCMultiUploader](https://github.com/I5UCC/VRCMultiUploader) but completely rewritten with a new UI and [multiplatform](https://creators.vrchat.com/avatars/per-platform-avatar-overrides) support.

## Install
1. [Click here](vcc://vpm/addRepo?url=https%3A%2F%2Fpurpzie.com/vpm) to add the VPM repository to the VRChat Creator Companion.
   - If that doesn't work, then in the VCC, go to Settings > Packages > Add Repository, paste [`https://purpzie.com/vpm/index.json`](https://purpzie.com/vpm/index.json) and click Add.
2. Make sure `Purpzie's VCC Packages` is checked.
3. Go back and click `Manage Project` next to your project, then add Bulk Avatar Uploader.
