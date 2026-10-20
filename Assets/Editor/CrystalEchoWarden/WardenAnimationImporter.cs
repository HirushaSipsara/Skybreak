using UnityEditor;
using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.WardenEditor
{
    // Scoped to Sachini's model; no import changes to other team assets.
    public sealed class WardenAnimationImporter : AssetPostprocessor
    {
        const string ModelPath = "Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx";
        public override uint GetVersion() { return 1; }
        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        }
        void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                if (!MotionChoice.TryName(clip.name, out var kind)) continue;
                clip.name = "CEW_" + kind;
                clip.loopTime = MotionChoice.Loops(kind);
                clip.wrapMode = clip.loopTime ? WrapMode.Loop : WrapMode.ClampForever;
            }
            importer.clipAnimations = clips;
        }
    }
}
