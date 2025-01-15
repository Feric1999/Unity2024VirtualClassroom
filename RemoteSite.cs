using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Agora.Rtc;
using RingBuffer;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.Networking;
using Unity.XR.CoreUtils;
using Oculus.Interaction;



#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
using UnityEngine.Android;
#endif

public class RemoteSite : MonoBehaviour
{

    //address of Agora room info
    public string room_info_address = "https://raw.githubusercontent.com/Feric1999/data/main/room_data.txt";

    // Fill in your app ID.
    internal string _appID = "745561d6795943978f25fe00b177b80f";
    // Fill in your channel name.
    internal string _channelName = "Oculus Test";
    // Fill in the temporary token you obtained from Agora Console.
    internal string _token = "";
    // channel ID of video atlas share screen.
    public uint _channelId1 = 99;

    //address of info on number of remote and local viewer 
    public string viewer_info_address = "https://raw.githubusercontent.com/Feric1999/data/main/rows_data.txt";

    //internal VideoSurface LocalView
    internal IRtcEngine RtcEngine;

    internal byte[] VideoBuffer = new byte[0];
    private bool _needResize = true;
    public int _videoFrameWidth = 640;
    public int _videoFrameHeight = 480;
    public int CHANNEL = 2;
    public int PULL_FREQ_PER_SEC = 100;
    public int SAMPLE_RATE = 48000;
    internal int _count;
    internal int _writeCount;
    internal int _readCount;
    internal RingBuffer<float> _audioBuffer;
    internal AudioClip _audioClip;
    internal bool isPlaying = false;

    //number of total rows in the atlas
    internal int num_rows = 1;
    //number of wireframes that are in the program
    internal int num_wireframe = 1;
    private Texture2D _texture;
    private VideoSurface RemoteView;

    //used to hold right controller
    public GameObject anchor;
    public GameObject cameraRig;
    //used to hold first and second positions you want to place the frames
    internal Vector3 position1 = new Vector3(0, 0, 0);
    internal Vector3 position2 = new Vector3(0, 0, 0);
    Quaternion rotation1 = new Quaternion(0, 0, 0, 0);
    Quaternion rotation2 = new Quaternion(0, 0, 0, 0);
    bool position1ready = false;
    bool position2ready = false;

    public Texture2D table_texture;
    public Texture2D wireframe_texture;
    public Texture2D white_texture;

    public Material clear_material;
    public Material default_material;

    public GameObject tables;

    public MeshRenderer _table1;
    public MeshRenderer _table2;
    public MeshRenderer _table3;
    public MeshRenderer _table4;
    public MeshRenderer _table5;
    public MeshRenderer _table6;
    public MeshRenderer _table7;
    public MeshRenderer _table8;

    public RawImage table1;
    public RawImage table2;
    public RawImage table3;
    public RawImage table4;
    public RawImage table5;
    public RawImage table6;
    public RawImage table7;
    public RawImage table8;

    private TMP_Text borderBtnText;
    private bool isBorderOn;

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    private ArrayList permissionList = new ArrayList() {Permission.Microphone };
#endif

    // Start is called before the first frame update
    void Start()
    {

        SetupVideoSDKEngine();
        InitEventHandler();
        SetupUI();

        //get data from online and attach textures to the wireframes. Also set the offset
        StartCoroutine(GetURLData());

    }

    // Update is called once per frame
    void Update()
    {
        CheckPermissions();

        if (_needResize)
        {
            InitializeTexture();
            _needResize = false;

        }
        else if (VideoBuffer != null && VideoBuffer.Length != 0)
        {
            lock (VideoBuffer)
            {

                for (int i = 0; i < num_wireframe; i++)
                {

                    _texture.LoadRawTextureData(VideoBuffer);
                    _texture.Apply();


                }
            }
        }

        //used to control the location of the canvas overlay using a and b buttons
        if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
        {
            anchor.SetActive(true);

            //a button press ets first anchor
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                position1 = anchor.transform.position;
                position1ready = true;
                rotation1 = anchor.transform.rotation;

            }
            //b button press sets second anchot
            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                position2 = anchor.transform.position;
                position2ready = true;
                rotation2 = anchor.transform.rotation;

            }

            //once both anchors have been set create canvas between anchors
            if (position1ready == true && position2ready == true)
            {
                //set canvas location using anchors
                Vector3 calibratedPos = Vector3.Lerp(position1, position2, 0.5f);

                position1ready = false;
                position2ready = false;

                var canvas = gameObject;

                // adjust canvas transform Rotation
                Quaternion calibratedRot = Quaternion.Lerp(rotation1, rotation2, 0.5f);
                calibratedRot.x = 0;
                calibratedRot.z = 0;
                canvas.transform.rotation = calibratedRot;

                //set the canvas poition
                canvas.transform.position = calibratedPos + canvas.transform.forward * 3 + canvas.transform.up * -3;

                //set tables location
                tables.transform.position = calibratedPos;

            }

        }
        else
        {
            anchor.SetActive(false);
        }
        
    }

    private void CheckPermissions()
    {
#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
        foreach (string permission in permissionList)
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                Permission.RequestUserPermission(permission);
            }
        }
#endif
    }

    //helper fucntion used to get data for number of local/remote viewers and Agora room info from a URL
    IEnumerator GetURLData()
    {

        UnityWebRequest www = UnityWebRequest.Get(room_info_address);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            //Show results as text
            Debug.Log(www.downloadHandler.text);

            //break room data from file into parts then store as variables
            string[] lines = (www.downloadHandler.text).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            _appID = lines[0];
            _channelName = lines[1];
            _token = lines[2];

            Debug.Log(_appID);
            Debug.Log(_channelName);
            Debug.Log(_token);


        }

        www = UnityWebRequest.Get(viewer_info_address);
        yield return www.SendWebRequest();

        int num_viewers = 0;
        int num_total = 0;

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);

            //break number of viewers into parts then store as variables
            string[] lines = (www.downloadHandler.text).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            num_viewers = int.Parse(lines[0]);
            num_total = int.Parse(lines[1]);
            num_rows = int.Parse(lines[2]);
            num_wireframe = int.Parse(lines[3]);

            Debug.Log(num_viewers);
            Debug.Log(num_total);
            Debug.Log(num_rows);
            Debug.Log(num_wireframe);

        }

        //flip surfaces so images not backwards
        GameObject go = GameObject.Find("RemoteView");
        RemoteView = go.AddComponent<VideoSurface>();

        int images_per_row = num_total / num_rows;
        float increment = 1.0f / images_per_row;
        int curr_images = 0;

        float x = 1.0f - ((images_per_row - 1) * 0.125f);
        float y = 4.0f;
        float u = 0.1f;
        float v = 1.0f;

        //set attach textures
        for (int i = 0; i < num_wireframe; i++)
        {

            go = gameObject.GetNamedChild("Frame" + i);
            var rd = go.GetComponent<RawImage>();
            rd.texture = _texture;

            //calculate how many students in a rectangle
            int num_students =  1;

            Debug.Log("Frame" + i);

            rd.uvRect = new Rect(x, y, u * num_students, v);

            x += 0.125f * num_students;

            curr_images += num_students;

            if (curr_images % images_per_row == 0)
            {
                x = 1.0f - ((images_per_row - 1) * 0.125f);
                y -= 1.0f;

            }

        }

    }

    private void SetupUI()
    {

        GameObject go;

        go = GameObject.Find("Leave");
        go.GetComponent<Button>().onClick.AddListener(Leave);

        go = GameObject.Find("Join");
        go.GetComponent<Button>().onClick.AddListener(Join);

        go = GameObject.Find("Borders");
        go.GetComponent<Button>().onClick.AddListener(Borders);
        borderBtnText = go.GetComponentInChildren<TextMeshProUGUI>(true);
        borderBtnText.text = "Turn Borders Off";
        isBorderOn = true;

    }

    //function used to Initialize an IRtcEngine instance
    public virtual void SetupVideoSDKEngine()
    {
        RtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();
        // Specify the context configuration to initialize the created instance.

        /*RtcEngineContext context = new RtcEngineContext(_appID, 0,
        CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION,
        AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT, AREA_CODE.AREA_CODE_GLOB, null);*/

        RtcEngineContext context = new RtcEngineContext();
        context.appId = _appID;
        context.channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING;
        context.audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT;

        // Initialize the instance.
        RtcEngine.Initialize(context);

        var bufferLength = SAMPLE_RATE * CHANNEL; // 1-sec-length buffer
        _audioBuffer = new RingBuffer<float>(bufferLength, true);
        var canvas = gameObject;
        var aud = canvas.AddComponent<AudioSource>();
        SetupAudio(aud, "externalClip");
        //base.SetupVideoSDKEngine();
        InitializeTexture();
        RtcEngine.InitEventHandler(new UserEventHandler(this));
        RtcEngine.RegisterVideoFrameObserver(new RawAudioVideoEventHandler(this),
            VIDEO_OBSERVER_FRAME_TYPE.FRAME_TYPE_RGBA,
            VIDEO_MODULE_POSITION.POSITION_POST_CAPTURER |
            VIDEO_MODULE_POSITION.POSITION_PRE_RENDERER |
            VIDEO_MODULE_POSITION.POSITION_PRE_ENCODER,
            OBSERVER_MODE.RAW_DATA);
        RtcEngine.SetPlaybackAudioFrameParameters(SAMPLE_RATE, CHANNEL,
            RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_ONLY, 1024);
        RtcEngine.SetRecordingAudioFrameParameters(SAMPLE_RATE, CHANNEL,
            RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_ONLY, 1024);
        RtcEngine.SetMixedAudioFrameParameters(SAMPLE_RATE, CHANNEL, 1024);
        RtcEngine.SetEarMonitoringAudioFrameParameters(SAMPLE_RATE, CHANNEL,
            RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_ONLY, 1024);
        RtcEngine.RegisterAudioFrameObserver(new RawAudioEventHandler(this),
            AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_PLAYBACK |
            AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_RECORD |
            AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_MIXED |
            AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_BEFORE_MIXING |
            AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_EAR_MONITORING,
            OBSERVER_MODE.RAW_DATA);

    }

    private void InitializeTexture()
    {
        //GameObject go;
        for (int i = 0; i < num_wireframe; i++)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(_videoFrameWidth, _videoFrameHeight, TextureFormat.RGBA32, false);
                //_texture[i] = new Texture2D(_videoFrameWidth, _videoFrameHeight, TextureFormat.RGBA32, false);

            }
            else
            {
                _texture.Reinitialize(_videoFrameWidth, _videoFrameHeight, TextureFormat.RGBA32, false);
                //_texture[i].Reinitialize(_videoFrameWidth, _videoFrameHeight, TextureFormat.RGBA32, false);

            }

        }

    }

    private void InitEventHandler()
    {
        // Creates a UserEventHandler instance.
        UserEventHandler handler = new UserEventHandler(this);
        RtcEngine.InitEventHandler(handler);

    }

    public void Join()
    {

        // Enable the video module.
        RtcEngine.EnableVideo();

        //sets the video encoder options which control frame and bitrate
        SetVideoEncoderConfiguration();

        // Set channel media options
        ChannelMediaOptions options = new ChannelMediaOptions();
        // Automatically subscribe to all audio streams
        options.autoSubscribeAudio.SetValue(true);
        // Automatically subscribe to all video streams
        options.autoSubscribeVideo.SetValue(true);
        // Set the channel profile to live broadcast
        options.channelProfile.SetValue(CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_COMMUNICATION);
        //Set the user role as host
        //options.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
        options.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_AUDIENCE);

        // Join a channel.
        RtcEngine.JoinChannel(_token, _channelName, 0, options);
    }

    public void Leave()
    {
        RtcEngine.UnRegisterAudioFrameObserver();
        RtcEngine.UnRegisterVideoFrameObserver();

        // Leaves the channel.
        RtcEngine.LeaveChannel();
        // Disable the video modules.
        RtcEngine.DisableVideo();

        if (RemoteView != null)
        {

            // Stops rendering the remote video.
            RemoteView.SetEnable(false);

        }
        

    }

    //function used to turn border of texture on or off
    public void Borders()
    {
        GameObject go;

        if (isBorderOn)
        {

            //set attach textures
            for (int i = num_wireframe; i < 24; i++)
            {

                go = gameObject.GetNamedChild("Frame" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = white_texture;


            }

            //set attach textures
            for (int i = 0; i < 62; i++)
            {

                go = gameObject.GetNamedChild("Real" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = white_texture;

            }

            //set attach textures
            for (int i = 1; i <= 8; i++)
            {

                go = gameObject.GetNamedChild("Table" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = white_texture;

            }

            _table1.material = clear_material;
            _table2.material = clear_material;
            _table3.material = clear_material;
            _table4.material = clear_material;
            _table5.material = clear_material;
            _table6.material = clear_material;
            _table7.material = clear_material;
            _table8.material = clear_material;

            borderBtnText.text = "Turn Borders On";
            isBorderOn = false;

        }
        else
        {

            //set attach textures
            for (int i = num_wireframe; i < 24; i++)
            {

                go = gameObject.GetNamedChild("Frame" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = wireframe_texture;


            }

            //set attach textures
            for (int i = 0; i < 62; i++)
            {

                go = gameObject.GetNamedChild("Real" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = wireframe_texture;

            }

            //set attach textures
            for (int i = 1; i <= 8; i++)
            {

                go = gameObject.GetNamedChild("Table" + i);
                var rd = go.GetComponent<RawImage>();
                rd.texture = table_texture;

            }

            _table1.material = default_material;
            _table2.material = default_material;
            _table3.material = default_material;
            _table4.material = default_material;
            _table5.material = default_material;
            _table6.material = default_material;
            _table7.material = default_material;
            _table8.material = default_material;

            borderBtnText.text = "Turn Borders Off";
            isBorderOn = true;

        }

    }

    internal static float[] ConvertByteToFloat16(byte[] byteArray)
    {
        var floatArray = new float[byteArray.Length / 2];
        for (var i = 0; i < floatArray.Length; i++)
        {
            floatArray[i] = BitConverter.ToInt16(byteArray, i * 2) / 32768f; // -Int16.MinValue
        }

        return floatArray;
    }

    void SetupAudio(AudioSource aud, string clipName)
    {
        _audioClip = AudioClip.Create(clipName,
            SAMPLE_RATE / PULL_FREQ_PER_SEC * CHANNEL,
            CHANNEL, SAMPLE_RATE, true,
            OnAudioRead);
        aud.clip = _audioClip;
        aud.loop = true;
        if (isPlaying)
        {
            aud.Play();
        }
    }

    private void OnAudioRead(float[] data)
    {
        lock (_audioBuffer)
        {
            for (var i = 0; i < data.Length; i++)
            {
                if (_audioBuffer.Count > 0)
                {
                    data[i] = _audioBuffer.Get();
                    _readCount += 1;
                }
            }
            Debug.Log(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], data[8]));
        }

        Debug.LogFormat("buffer length remains: {0}", _writeCount - _readCount);
    }

    public void SetVideoEncoderConfiguration()
    {
        VideoEncoderConfiguration config = new VideoEncoderConfiguration();
        config.dimensions = new VideoDimensions(_videoFrameWidth, _videoFrameHeight);
        // Sets the video frame rate.
        config.frameRate = 15;
        // Sets the video encoding bitrate (Kbps).
        config.bitrate = 800;
        // Sets the adaptive orientation mode. See the description in API Reference.
        config.orientationMode = ORIENTATION_MODE.ORIENTATION_MODE_ADAPTIVE;
        // Sets the video encoding degradation preference under limited bandwidth. MIANTAIN_QUALITY means to degrade the frame rate to maintain the video quality.
        config.degradationPreference = DEGRADATION_PREFERENCE.MAINTAIN_FRAMERATE;
        // Sets the video encoder configuration.
        RtcEngine.SetVideoEncoderConfiguration(config);
    }

    internal class UserEventHandler : IRtcEngineEventHandler
    {
        private readonly RemoteSite _videoSample;

        internal UserEventHandler(RemoteSite videoSample)
        {
            _videoSample = videoSample;
        }

        // error callback
        public override void OnError(int err, string msg)
        {
        }

        // This callback is triggered when a remote user leaves the channel or drops offline.
        public override void OnUserOffline(RtcConnection connection, uint uid, USER_OFFLINE_REASON_TYPE reason)
        {

            _videoSample.RemoteView.SetEnable(false);

        }

        // This callback is triggered when the local user joins the channel.
        public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
        {
            Debug.Log("You joined channel: " + connection.channelId);
        }

        public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
        {
            // Save the remote user ID in a variable.
            //_videoSample.remoteUid = 99;
            // Setup remote view.
            _videoSample.RemoteView.SetForUser(_videoSample._channelId1, connection.channelId, VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
            _videoSample.RemoteView.SetEnable(true);
            Debug.Log("Remote user joined");


        }

    }

    // Internal class for handling audio events
    internal class RawAudioEventHandler : IAudioFrameObserver
    {
        private RemoteSite _agoraAudioRawData;

        internal RawAudioEventHandler(RemoteSite agoraAudioRawData)
        {
            _agoraAudioRawData = agoraAudioRawData;
        }

        public override bool OnRecordAudioFrame(string channelId, AudioFrame audioFrame)
        {
            var floatArray = RemoteSite.ConvertByteToFloat16(audioFrame.RawBuffer);

            lock (_agoraAudioRawData._audioBuffer)
            {
                _agoraAudioRawData._audioBuffer.Put(floatArray);
                _agoraAudioRawData._writeCount += floatArray.Length;
                _agoraAudioRawData._count++;
            }
            return true;
        }
        public override bool OnPlaybackAudioFrame(string channelId, AudioFrame audioFrame)
        {
            return true;
        }
        public override bool OnPlaybackAudioFrameBeforeMixing(string channel_id, uint uid, AudioFrame audio_frame)
        {
            return false;
        }

        public override bool OnPlaybackAudioFrameBeforeMixing(string channel_id,
        string uid,
        AudioFrame audio_frame)
        {
            return false;
        }
    }

    // Internal class for handling media player events
    internal class RawAudioVideoEventHandler : IVideoFrameObserver
    {
        private RemoteSite rawAudioVideoManager;

        internal RawAudioVideoEventHandler(RemoteSite refRawAudioVideoManager)
        {
            rawAudioVideoManager = refRawAudioVideoManager;
        }

        public override bool OnCaptureVideoFrame(VIDEO_SOURCE_TYPE type, VideoFrame videoFrame)
        {
            rawAudioVideoManager._videoFrameWidth = videoFrame.width;
            rawAudioVideoManager._videoFrameHeight = videoFrame.height;
            return true;
        }

        public override bool OnRenderVideoFrame(string channelId, uint uid, VideoFrame videoFrame)
        {
            if (uid == rawAudioVideoManager._channelId1)
            {

                if (rawAudioVideoManager._videoFrameWidth != videoFrame.width || rawAudioVideoManager._videoFrameHeight != videoFrame.height)
                {

                    rawAudioVideoManager._videoFrameWidth = videoFrame.width;
                    rawAudioVideoManager._videoFrameHeight = videoFrame.height;
                    rawAudioVideoManager._needResize = true;

                }

                lock (rawAudioVideoManager.VideoBuffer)
                {
                    rawAudioVideoManager.VideoBuffer = videoFrame.yBuffer;
                }

            }
            return true;
        }
    }

    void OnApplicationQuit()
    {
        if (RtcEngine != null)
        {
            Leave();
            RtcEngine.UnRegisterVideoFrameObserver();
            RtcEngine.UnRegisterAudioFrameObserver();
            RtcEngine.Dispose();
            RtcEngine = null;
        }

    }

}
