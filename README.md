# WeChat Mini Game Unity/Tuanjie Engine SDK (English versiion)

For the latest features and usage of the WeChat SDK, please read [Unity WebGL WeChat Mini Game Adaptation Solution](https://wechat-miniprogram.github.io/minigame-unity-webgl-transform/).

## Installation Guide

Create/open your game project using Unity Engine or [Tuanjie Engine](https://unity.cn/tuanjie/tuanjieyinqing),
Go to Unity Editor menu bar `Window` - `Package Manager` - `+ button in top right` - `Add package from git URL...` and enter this repository's Git URL.

For example: `https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git`

## Common Issues

#### 1. Game project can be exported but shows errors when running in WeChat Developer Tools:
This commonly occurs in cases such as empty projects or when the game code has never used any Runtime capabilities of WXSDK. The Tuanjie Engine will trim the WeChat Runtime package during export. The solution is to add appropriate usage of WXSDK in your game code.
