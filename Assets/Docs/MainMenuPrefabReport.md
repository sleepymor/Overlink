# Legacy Main Menu Prefab Report

> Asset: `Assets/Prefabs/MainMenu.prefab`  
> Status: **Legacy-only; not used by the active player flow**  
> Active replacement: `Assets/Scenes/MainMenu.unity`

## Decision

The active Main Menu is the authored `Assets/Scenes/MainMenu.unity` scene. The prefab remains in the
project only as a historical/compatibility asset and must not be used as the source for the shipped Main
Menu. No scene in `Assets/Scenes` references `Assets/Prefabs/MainMenu.prefab`.

## Serialized inspection

The prefab contains the older command surface (`MainMenu`, `ButtonPanel`, `Btn_VS_AI`, `Btn_Matchmaking`,
`Btn_Settings`, `Logo`, and `VersionLabel`) but does not contain the authored account/profile hierarchy
shipped by `MainMenu.unity`.

Its `MainMenuController` account/profile fields are unassigned:

- `playerNameText: {fileID: 0}`
- `btnProfile: {fileID: 0}`
- `btnSignOut: {fileID: 0}`
- `profilePanel: {fileID: 0}`
- `txtProfileUsername: {fileID: 0}`
- `txtProfileTag: {fileID: 0}`
- `txtProfileFullID: {fileID: 0}`
- `btnCopyUsername: {fileID: 0}`
- `btnCopyTag: {fileID: 0}`
- `btnCopyID: {fileID: 0}`
- `btnCloseProfile: {fileID: 0}`
- `copyFeedback: {fileID: 0}`

## Runtime rule

Do not instantiate this prefab for the player-facing Main Menu. The active scene owns the authored layout,
profile panel, account controls, and interaction targets. The serialized UI integrity audit covers the active
scene and does not treat this legacy prefab as a shipped screen.
