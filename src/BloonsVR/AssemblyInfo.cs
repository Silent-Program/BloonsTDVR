using MelonLoader;

// MelonLoader finds Melons by scanning MelonLoader/net6 + Mods for MelonMod subclasses.
// Argument order is (type, name, version, author) - the log prints it back and will happily accept a
// non-semver version if the last two are swapped.
[assembly: MelonInfo(typeof(BloonsVR.BloonsVRMod), "BloonsVR", "0.1.0", "BloonsVR")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]