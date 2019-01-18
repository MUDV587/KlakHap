using System;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Klak.Hap
{
    class HapDecoder : MonoBehaviour
    {
        [SerializeField] string _fileName = "Test.mov";

        IntPtr _hap;
        Thread _thread;

        AutoResetEvent _decodeRequest = new AutoResetEvent(false);
        int _decodeFrame;

        Texture2D _texture;
        CommandBuffer _command;

        static TextureFormat VideoTypeToTextureFormat(int type)
        {
            switch (type & 0xf)
            {
                case 0xb: return TextureFormat.DXT1;
                case 0xe: return TextureFormat.DXT5;
                case 0xf: return TextureFormat.DXT5;
                case 0xc: return TextureFormat.BC7;
                case 0x1: return TextureFormat.BC4;
            }
            return TextureFormat.DXT1;
        }

        void Start()
        {
            var path = Path.Combine(Application.streamingAssetsPath, _fileName);
            _hap = HapOpen(path);

            _thread = new Thread(DecoderThread);
            _thread.Start();

            _texture = new Texture2D(
                HapGetVideoWidth(_hap),
                HapGetVideoHeight(_hap),
                VideoTypeToTextureFormat(HapGetVideoType(_hap)),
                false
            );

            _command = new CommandBuffer();
        }

        void OnDestroy()
        {
            _decodeFrame = -1;
            _decodeRequest.Set();

            _thread.Join();

            if (_hap != IntPtr.Zero) HapClose(_hap);
            if (_texture != null) Destroy(_texture);
            if (_command != null) _command.Dispose();
        }

        void Update()
        {
            var index = Time.frameCount % HapCountFrames(_hap);

            _decodeFrame = index;
            _decodeRequest.Set();

            _command.IssuePluginCustomTextureUpdateV2(
                HapGetTextureUpdateCallback(), _texture, HapGetID(_hap)
            );
            Graphics.ExecuteCommandBuffer(_command);
            _command.Clear();

            GetComponent<Renderer>().material.mainTexture = _texture;
        }

        void DecoderThread()
        {
            while (true)
            {
                _decodeRequest.WaitOne();
                if (_decodeFrame < 0) break;
                HapDecodeFrame(_hap, _decodeFrame);
            }
        }

        [DllImport("KlakHap")]
        internal static extern IntPtr HapGetTextureUpdateCallback();

        [DllImport("KlakHap")]
        internal static extern IntPtr HapOpen(string filepath);

        [DllImport("KlakHap")]
        internal static extern void HapClose(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern uint HapGetID(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern int HapCountFrames(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern int HapGetVideoWidth(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern int HapGetVideoHeight(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern int HapGetVideoType(IntPtr context);

        [DllImport("KlakHap")]
        internal static extern void HapDecodeFrame(IntPtr context, int index);
    }
}
