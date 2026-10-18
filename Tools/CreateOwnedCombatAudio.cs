using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Skybreak.AI;

public static class CreateOwnedCombatAudio
{
    const string Dir = "Assets/Audio/Combat/OwnedAgents";
    const int Rate = 24000;
    // Filtered noise, short electrical cracks and inharmonic metal tails, rather than pitched drums.
    sealed class Texture
    {
        readonly System.Random random;
        float low, mid;
        public Texture(int seed) { random = new System.Random(seed); }
        public float Sample(CombatSound sound, float t, float duration)
        {
            float noise = (float)(random.NextDouble()*2-1), x=t/duration;
            low += .035f*(noise-low); mid += .25f*(noise-mid);
            float band=mid-low, air=noise-mid;
            float metal=(Mathf.Sin(t*2*Mathf.PI*1373)+Mathf.Sin(t*2*Mathf.PI*2197)+Mathf.Sin(t*2*Mathf.PI*3181))*.333f;
            float signal;
            switch (sound)
            {
                case CombatSound.PlayerShot:
                    signal=air*2.5f*Mathf.Exp(-t*170)+band*1.8f*Mathf.Exp(-t*40)
                        +Mathf.Sin(2*Mathf.PI*(2800*t-850*t*t/duration))*.18f*Mathf.Exp(-t*65)
                        +metal*.10f*Mathf.Exp(-t*30); break;
                case CombatSound.CannonShot:
                    signal=air*2.2f*Mathf.Exp(-t*125)+band*3*Mathf.Exp(-t*20)
                        +low*2.8f*Mathf.Exp(-t*12)+metal*.20f*Mathf.Exp(-t*16); break;
                case CombatSound.Teleport:
                    signal=(band*2+air*.12f)*(Mathf.Pow(Mathf.Sin(Mathf.PI*x),1.3f))
                        +metal*.13f*Mathf.Sin(Mathf.PI*x)+air*.6f*Mathf.Exp(-Mathf.Abs(t-.18f)*100); break;
                case CombatSound.Slash:
                    signal=(air*.6f+band*2.5f)*Mathf.Sin(Mathf.PI*x)+metal*.12f*Mathf.Exp(-t*15); break;
                case CombatSound.Hit:
                    signal=band*3.5f*Mathf.Exp(-t*35)+air*.9f*Mathf.Exp(-t*100)+low*1.8f*Mathf.Exp(-t*22); break;
                case CombatSound.Jump:
                case CombatSound.DoubleJump:
                    signal=(band*2+air*.20f)*Mathf.Sin(Mathf.PI*x)
                        +(sound==CombatSound.DoubleJump?metal*.12f*Mathf.Exp(-t*18):0); break;
                case CombatSound.Land:
                    signal=band*2.5f*Mathf.Exp(-t*40)+low*3*Mathf.Exp(-t*30); break;
                case CombatSound.ArmBreak:
                    signal=air*Mathf.Exp(-t*50)+band*2*Mathf.Exp(-t*12)
                        +metal*.5f*Mathf.Exp(-t*8)+air*.35f*Mathf.Exp(-Mathf.Abs(t-.13f)*130); break;
                case CombatSound.CoreOpen:
                    signal=(band*1.8f+low*2)*Mathf.Sin(Mathf.PI*x)
                        +metal*.18f*Mathf.Pow(Mathf.Sin(Mathf.PI*x),2); break;
                case CombatSound.Defeat:
                    signal=(band*2.8f+low*3.5f)*Mathf.Exp(-t*5)+metal*.3f*Mathf.Exp(-t*8)+air*Mathf.Exp(-t*90); break;
                default:
                    signal=band*1.5f*Mathf.Sin(Mathf.PI*x)+metal*.05f*Mathf.Sin(Mathf.PI*x); break;
            }
            float envelope=Mathf.Min(1,t*1500)*Mathf.Min(1,(duration-t)*100);
            return (float)Math.Tanh(signal)*envelope*.68f;
        }
    }
    static void Wave(string path,int kind,float length)
    {
        int samples=(int)(Rate*length); var texture=new Texture(8400+kind);
        using(var stream=File.Create(path)) using(var writer=new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(Rate);writer.Write(Rate*2);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
            for(int i=0;i<samples;i++)writer.Write((short)(texture.Sample((CombatSound)kind,(float)i/Rate,length)*32767));
        }
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before creating/importing audio");
        Directory.CreateDirectory(Dir); float[] lengths={.22f,.42f,.42f,.24f,.20f,.20f,.27f,.16f,.55f,.65f,.85f,.28f};
        for(int i=0;i<lengths.Length;i++)Wave(Dir+"/"+((CombatSound)i)+".wav",i,lengths[i]);AssetDatabase.Refresh();
        var clips=new AudioClip[lengths.Length];
        for(int i=0;i<clips.Length;i++)
        {
            string path=Dir+"/"+((CombatSound)i)+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();clips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        string bankPath=Dir+"/CombatSoundBank.asset";var bank=AssetDatabase.LoadAssetAtPath<CombatSoundBank>(bankPath);if(bank==null){bank=ScriptableObject.CreateInstance<CombatSoundBank>();AssetDatabase.CreateAsset(bank,bankPath);}
        bank.clips=clips;EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();return "12 redesigned mono effects: electrical shot cracks, filtered cannon impact, dark portal/metal textures and air bursts; 24kHz PCM, about 209KiB sample payload. Existing asset GUIDs preserved.";
    }
}
