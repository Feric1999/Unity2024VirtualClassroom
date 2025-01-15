using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Agora.Rtc;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Networking;
using System;

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
using UnityEngine.Android;
#endif

public class VideoAtlasClick : MonoBehaviour
{
    //address of Agora room info
    public string room_info_address = "https://raw.githubusercontent.com/Feric1999/data/main/room_data.txt";

    // Fill in your app ID.
    internal string _appID = "745561d6795943978f25fe00b177b80f";
    // Fill in your channel name.
    internal string _channelName = "Oculus Test";
    // Fill in the temporary token you obtained from Agora Console.
    internal string _token = "";

    //store channel Ids for share screen and instructor webcam
    public uint _channelId1 = 99;
    public uint _channelId2 = 100;

    //address of info on number of remote and local viewer 
    public string viewer_info_address = "https://raw.githubusercontent.com/Feric1999/data/main/rows_data.txt";

    //number of remote viewers
    internal int num_viewers = 0;
    //number of total viewers
    internal int num_total = 0;
    //number of rows
    internal int num_rows = 0;
    //number of wireframes in oculus classroom
    internal int num_wireframe = 0;

    // A variable to save the remote user uid.
    private uint[] remoteUid = new uint[100];

    //material used to apply to Raw Images
    public Material material;
    public int Render_width = 150;
    public int Render_height = 150;

    //Modify Camera
    public Camera curr_camera;

    internal VideoSurface[] Views = new VideoSurface[100];

    //create two RtcEngines one to send share screen the other to send web camera to agora
    internal IRtcEngine RtcEngine;
    internal RtcConnection webcam_connection;
    internal RtcConnection share_screen_connection;

    // In a real world app, you declare the media location variable with an empty string
    // and update it when a user chooses a media file from a local or remote source.
    public string mediaLocation = "https://www.cs.purdue.edu/cgvlab/popescu/HERE/Feb17/";
    private IMediaPlayer[] mediaPlayer = new IMediaPlayer[100]; // Instance of the media player
    private bool isMediaPlaying = false;
    private long mediaDuration = 0;
    private TMP_Text mediaBtnText;
    private Slider[] mediaProgressBar = new Slider[100];

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    private ArrayList permissionList = new ArrayList() { Permission.Camera};
#endif


    //helper function used to create a image surface
    private VideoSurface MakeImageVideoSurface(GameObject go, float x, float y, float z)
    {

        RawImage curr_raw = go.AddComponent<RawImage>();

        curr_raw.material = material;

        //curr_raw.material.SetTextureOffset("_MainTex", new Vector2(0.5f, 0.0f));

        curr_raw.transform.Rotate(0.0f, 180.0f, 0.0f);

        go.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        var rectTransform = go.GetComponent<RectTransform>();

        rectTransform.sizeDelta = new Vector2(Render_width, Render_height);
        rectTransform.localPosition = new Vector3(rectTransform.localPosition.x + x, rectTransform.localPosition.y + y, rectTransform.localPosition.z + z);

        return go.AddComponent<VideoSurface>();

    }

    //helper function used to create a slider for the media players
    private Slider MakeSlider(GameObject go, float x, float y, float z)
    {

        Slider curr_slider = go.GetComponent<Slider>();

        var rectTransform = go.GetComponent<RectTransform>();

        rectTransform.localPosition = new Vector3(rectTransform.localPosition.x + x, rectTransform.localPosition.y + y - Render_height - 500, rectTransform.localPosition.z + z);

        return curr_slider;

    }

    //used to update channel publish options
    private void updateChannelPublishOptions(bool publishMediaPlayer)
    {
        ChannelMediaOptions channelOptions = new ChannelMediaOptions();
        channelOptions.publishMediaPlayerAudioTrack.SetValue(publishMediaPlayer);
        channelOptions.publishMediaPlayerVideoTrack.SetValue(publishMediaPlayer);
        channelOptions.publishMicrophoneTrack.SetValue(!publishMediaPlayer);
        channelOptions.publishCameraTrack.SetValue(!publishMediaPlayer);
        if (publishMediaPlayer)
        {

            int remote_surfaces_generated = 0;

            //loop through all local (video) students
            for (int i = 0; i < num_total; i++)
            {

                if (remote_surfaces_generated >= num_viewers)
                {

                    channelOptions.publishMediaPlayerId.SetValue(mediaPlayer[i].GetId());
                


                }
                else
                {

                    remote_surfaces_generated++;

                }

            }
        }

        RtcEngine.UpdateChannelMediaOptions(channelOptions);

    }

    //function used to attach mediaplayer objects to surfaces that show local video
    private void setupLocalVideo(bool forMediaPlayer)
    {
        GameObject go;

        GameObject canvas = GameObject.Find("Canvas");

        int remote_surfaces_generated = 0;

        //loop through all local (video) students 
        for (int i = 0; i < num_total; i++)
        {
            if (remote_surfaces_generated >= num_viewers)
            {

                if (forMediaPlayer)
                {

                    go = GameObject.Find("Viewer" + i);

                    Views[i] = go.AddComponent<VideoSurface>();
                    go.transform.Rotate(0.0f, 180.0f, 0.0f);

                    Views[i].SetForUser((uint)mediaPlayer[i].GetId(), "", VIDEO_SOURCE_TYPE.VIDEO_SOURCE_MEDIA_PLAYER);

                }

            }
            else
            {

                remote_surfaces_generated++;

            }

        }

    }

    //function that is attached to the "Play Media" button and is used to play the local videos
    public void playMedia()
    {
        // Initialize the mediaPlayer and open a media file
        bool initialized = false;

        int remote_surfaces_generated = 0;

        //loop through all local (video) students 
        for (int i = 0; i < num_total; i++)
        {
            if (remote_surfaces_generated >= num_viewers)
            {

                if (mediaPlayer[i] == null)
                {
                    // Create an instance of the media player
                    mediaPlayer[i] = RtcEngine.CreateMediaPlayer();
                    // Create an instance of mediaPlayerObserver class
                    MediaPlayerObserver mediaPlayerObserver = new MediaPlayerObserver(this);
                    // Set the mediaPlayerObserver to receive callbacks
                    mediaPlayer[i].InitEventHandler(mediaPlayerObserver);
                    // Open the media file
                    mediaPlayer[i].Open(Path.Combine(mediaLocation, "video" + (i % 10) + ".mp4"), 0);
                    mediaBtnText.text = "Opening Media File...";
                    initialized = true;

                }

            }
            else
            {

                remote_surfaces_generated++;

            }

        }

        if (initialized)
        {

            return;

        }

        // Set up the local video container to handle the media player output
        // or the camera stream, alternately.
        isMediaPlaying = !isMediaPlaying;
        // Set the stream publishing options
        //updateChannelPublishOptions(isMediaPlaying);
        // Display the stream locally
        setupLocalVideo(isMediaPlaying);

        remote_surfaces_generated = 0;

        //loop through all local (video) students 
        for (int i = 0; i < num_total; i++)
        {
            if (remote_surfaces_generated >= num_viewers)
            {

                MEDIA_PLAYER_STATE state = mediaPlayer[i].GetState();
                if (isMediaPlaying)
                { // Start or resume playing media
                    if (state == MEDIA_PLAYER_STATE.PLAYER_STATE_OPEN_COMPLETED)
                    {
                        mediaPlayer[i].Play();
                    }
                    else if (state == MEDIA_PLAYER_STATE.PLAYER_STATE_PAUSED)
                    {
                        mediaPlayer[i].Resume();
                    }
                    mediaBtnText.text = "Pause Playing Media";
                }
                else
                {
                    if (state == MEDIA_PLAYER_STATE.PLAYER_STATE_PLAYING)
                    {
                        // Pause media file
                        mediaPlayer[i].Pause();
                        mediaBtnText.text = "Resume Playing Media";
                    }
                }
            }
            else
            {

                remote_surfaces_generated++;

            }

        }
    }

    //used to send camera to agora
    private void PreviewSelf()
    {
        // Enable the video module
        RtcEngine.EnableVideo();
        // Enable local video preview
        RtcEngine.StartPreview(Agora.Rtc.VIDEO_SOURCE_TYPE.VIDEO_SOURCE_CAMERA_SECONDARY);
    }

    // Start is called before the first frame update
    void Start()
    {

        //File.Create(Application.persistentDataPath + "//test.txt");
        SetupVideoSDKEngine();
        InitEventHandler();
        SetupUI();

        for (int i = 0; i < 100; i++)
        {

            remoteUid[i] = 0;

        }
  
    }

    // Update is called once per frame
    void Update()
    {
        //call function to set permissions
        CheckPermissions();

        //inputs used to control camera on Window
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            curr_camera.fieldOfView += 1;

        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            curr_camera.fieldOfView -= 1;

        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            curr_camera.transform.Translate(0, 30, 0);

        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            curr_camera.transform.Translate(-30, 0, 0);

        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            curr_camera.transform.Translate(0, -30, 0);

        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            curr_camera.transform.Translate(30, 0, 0);

        }

    }

    //function used to check permissions
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
    //Then the helper function takes the data inputs then generates the wireframes/surfaces to display the code
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

        GameObject canvas = GameObject.Find("Canvas");
        GameObject aSlider = GameObject.Find("Slider");

        float distx = 90 - Render_width;
        float disty = 90 - Render_height;

        float start_x = canvas.transform.position.x - 420;
        float start_y = canvas.transform.position.y + 700;
        float start_z = canvas.transform.position.z;

        float curr_x = start_x;
        float curr_y = start_y;
        float curr_z = start_z;

        int remote_surfaces_generated = 0;

        //create the rows of images
        for (int i = 0; i < num_rows; i++)
        {

            int images_per_row = num_total / num_rows;

            //loops until limit for number of images per row reached
            for (int j = 0; j < images_per_row && (i * images_per_row + j) < num_total; j++)
            {

                GameObject curr_view = new GameObject("Viewer" + (i * images_per_row + j));
                // Update the VideoSurface component of the local view object.
                Views[(i * images_per_row + j)] = MakeImageVideoSurface(curr_view, curr_x, curr_y, curr_z);
                Views[(i * images_per_row + j)].transform.Rotate(0.0f, 0.0f, 180.0f);

                curr_view.transform.SetParent(canvas.transform);

                //check if image should be a remote or a local (video) user
                if (remote_surfaces_generated >= num_viewers)
                {

                    GameObject curr_slider = Instantiate(aSlider);
                    curr_slider.name = "Slider" + (i * images_per_row + j);

                    mediaProgressBar[num_total] = MakeSlider(curr_slider, curr_x, curr_y, curr_z);
                    curr_slider.transform.SetParent(canvas.transform);

                }
                else
                {

                    remote_surfaces_generated++;

                }

                curr_x = curr_x + Render_width + distx;
            }

            curr_x = start_x;

            curr_y = curr_y - (Render_height + disty);

        }

    }

    //function used to set up local UI
    private void SetupUI()
    {
        StartCoroutine(GetURLData());

        // find and attach buttons
        GameObject go = GameObject.Find("Leave");
        go.GetComponent<Button>().onClick.AddListener(Leave);

        go = GameObject.Find("Join");
        go.GetComponent<Button>().onClick.AddListener(Join);

        go = GameObject.Find("playMedia");
        go.GetComponent<Button>().onClick.AddListener(playMedia);
        mediaBtnText = go.GetComponentInChildren<TextMeshProUGUI>(true);
        mediaBtnText.text = "Play Video";
        //mediaBtnText.fontSize = 120;

    }

    //function used to Initialize an IRtcEngine instance
    private void SetupVideoSDKEngine()
    {
        //create first Rtc engine to share screen
        RtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngineEx();

        // Specify the context configuration to initialize the created instance.
        RtcEngineContext context = new RtcEngineContext();
        context.appId = _appID;
        context.channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING;
        context.audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT;

        // Initialize the instance.
        RtcEngine.Initialize(context);

    }

    private void InitEventHandler()
    {
        // Creates a UserEventHandler instance.
        UserEventHandler handler = new UserEventHandler(this);
        RtcEngine.InitEventHandler(handler);

    }

    //helper function used to join a room twice one to push the webcam and the other to push the screen share. Attached to the "Join" button
    public void Join()
    {

        /* note to self the order of the join calls/setting the media options matters a great deal. Each of the two join channels must come before the webcam capture/screen capture function calls. 
         Aditionally the calls for the capture must come before the join call for the other capture. There fore there is a set order*/

        //channel media options are very important and decide what parts of the agora video call works. Look into channel options if things seem to not be working

        RtcEngine.RegisterAudioFrameObserver(new AudioFrameObserver(this), AUDIO_FRAME_POSITION.AUDIO_FRAME_POSITION_NONE, OBSERVER_MODE.INTPTR);
        RtcEngine.RegisterVideoFrameObserver(new VideoFrameObserver(this), VIDEO_OBSERVER_FRAME_TYPE.FRAME_TYPE_RGBA, VIDEO_MODULE_POSITION.POSITION_POST_CAPTURER, OBSERVER_MODE.INTPTR);

        // Set the format of the captured raw audio data.
        int SAMPLE_RATE = 16000, SAMPLE_NUM_OF_CHANNEL = 1, SAMPLES_PER_CALL = 1024;

        RtcEngine.SetRecordingAudioFrameParameters(SAMPLE_RATE, SAMPLE_NUM_OF_CHANNEL,
        RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_WRITE, SAMPLES_PER_CALL);
        RtcEngine.SetPlaybackAudioFrameParameters(SAMPLE_RATE, SAMPLE_NUM_OF_CHANNEL,
        RAW_AUDIO_FRAME_OP_MODE_TYPE.RAW_AUDIO_FRAME_OP_MODE_READ_WRITE, SAMPLES_PER_CALL);
        //RtcEngine.SetMixedAudioFrameParameters(SAMPLE_RATE, SAMPLE_NUM_OF_CHANNEL, SAMPLES_PER_CALL);

        // Enable the video module for web camera
        RtcEngine.EnableAudio();
        RtcEngine.EnableVideo();
        RtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);

        webcam_connection = new RtcConnection(_channelName, _channelId2);

        //set channel options for share screen
        ChannelMediaOptions options = new ChannelMediaOptions();
        options.publishCameraTrack.SetValue(false);
        options.publishScreenTrack.SetValue(true);

#if UNITY_ANDROID || UNITY_IPHONE
  options.publishScreenCaptureAudio.SetValue(true);
  options.publishScreenCaptureVideo.SetValue(true);
#endif
        RtcEngine.UpdateChannelMediaOptions(options);

        var ret = RtcEngine.JoinChannel(_token, _channelName, _channelId1, options);

        //calls function to start sharing screen
        StartSharing();

        //sets the video encoder options which control frame and bitrate
        SetVideoEncoderConfiguration();

        curr_camera.transform.Translate(0, 5, 0);
        curr_camera.transform.Translate(-5, 0, 0);

        for (int i = 0; i < 3; i++)
        {

            curr_camera.transform.Translate(-30, 0, 0);

        }

        for (int i = 0; i < 15; i++)
        {

            curr_camera.fieldOfView -= 1;

        }

        for (int i = 0; i < 8; i++)
        {

            curr_camera.transform.Translate(0, 30, 0);

        }


    }
    //helper function used to join a room. Attached to the "Leave" button
    public void Leave()
    {
        RtcEngine.UnRegisterAudioFrameObserver();
        RtcEngine.UnRegisterVideoFrameObserver();

        //stop share screen
        StopSharing();
        // Leaves the channel.
        RtcEngine.LeaveChannel();
        // Disable the video modules.
        RtcEngine.DisableVideo();

        int remote_surfaces_generated = 0;

        //loop through all local (video) students 
        for (int i = 0; i < num_total; i++)
        {
            //stops rendering videos
            if (remote_surfaces_generated >= num_viewers)
            {

                Views[i].SetEnable(false);

            }
            else
            {
                Views[i].SetEnable(false);
                remote_surfaces_generated++;

            }
        }

    }

    // Get the list of shareable screens
    private ScreenCaptureSourceInfo[] GetScreenCaptureSources()
    {
        SIZE targetSize = new SIZE(360, 660);
        return RtcEngine.GetScreenCaptureSources(targetSize, targetSize, true);
    }

    private void StartScreenCaptureMobile(long sourceId)
    {
        // Configure screen capture parameters for Android.
        var parameters2 = new ScreenCaptureParameters2();
        parameters2.captureAudio = true;
        parameters2.captureVideo = true;
        // Start screen sharing.
        RtcEngine.StartScreenCapture(parameters2);
    }

    private void StartScreenCaptureWindows(ulong sourceId)
    {
        // Configure screen capture parameters for Windows.
        RtcEngine.StartScreenCaptureByDisplayId((uint)sourceId, default(Rectangle),
            new ScreenCaptureParameters { captureMouseCursor = true, frameRate = 30 });
    }
    // Share the screen
    public void StartSharing()
    {
        if (RtcEngine == null)
        {
            Debug.Log("Join a channel to start screen sharing");
            return;
        }

        // Get a list of shareable screens and windows.
        var captureSources = GetScreenCaptureSources();

        if (captureSources != null && captureSources.Length > 0)
        {
            var sourceId = captureSources[0].sourceId;

            StartScreenCaptureWindows(sourceId);

            // Publish the screen track.
            PublishScreenTrack();
            
        }
        else
        {
            Debug.LogWarning("No screen capture sources found.");
        }
    }

    public void PublishScreenTrack()
    {
        // Publish the screen track
        ChannelMediaOptions options = new ChannelMediaOptions();
        options.publishCameraTrack.SetValue(false);
        options.publishScreenTrack.SetValue(true);

#if UNITY_ANDROID || UNITY_IPHONE
        options.publishScreenCaptureAudio.SetValue(true);
        options.publishScreenCaptureVideo.SetValue(true);
#endif
        RtcEngine.UpdateChannelMediaOptions(options);
    }

    public void UnPublishScreenTrack()
    {
        // Unpublish the screen track.
        ChannelMediaOptions channelOptions = new ChannelMediaOptions();
        channelOptions.publishScreenTrack.SetValue(false);
        channelOptions.publishCameraTrack.SetValue(true);
        channelOptions.publishMicrophoneTrack.SetValue(true);
        RtcEngine.UpdateChannelMediaOptions(channelOptions);
    }

    public void StopSharing()
    {
        // Stop screen sharing.
        RtcEngine.StopScreenCapture();

        // Publish the local video track when you stop sharing your screen.
        UnPublishScreenTrack();

    }

    public void SetVideoEncoderConfiguration()
    {
        VideoEncoderConfiguration config = new VideoEncoderConfiguration();
        // Sets the video resolution.
        config.dimensions.width = 640;
        config.dimensions.height = 480;

        // Sets the video frame rate.
        config.frameRate = 10;
        // Sets the video encoding bitrate (Kbps).
        config.bitrate = 65;
        // Sets the adaptive orientation mode. See the description in API Reference.
        config.orientationMode = ORIENTATION_MODE.ORIENTATION_MODE_ADAPTIVE;
        // Sets the video encoding degradation preference under limited bandwidth. MIANTAIN_QUALITY means to degrade the frame rate to maintain the video quality.
        config.degradationPreference = DEGRADATION_PREFERENCE.MAINTAIN_FRAMERATE;
        // Sets the video encoder configuration.
        RtcEngine.SetVideoEncoderConfiguration(config);
    }

    internal class UserEventHandler : IRtcEngineEventHandler
    {
        private readonly VideoAtlasClick _videoSample;

        internal UserEventHandler(VideoAtlasClick videoSample)
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

            //check whether the current users joining is greater than the total allowed number of viewers
            if ((uid != _videoSample._channelId1) && (uid != _videoSample._channelId2))
            {
                //lots of erors need to be fixed

                for (int i = 0; i < _videoSample.num_total; i++)
                {

                    if (_videoSample.remoteUid[i] == uid)
                    {

                        _videoSample.Views[i].SetEnable(false);
                        _videoSample.remoteUid[i] = 0;
                        return;

                    }

                }

            }

        }

        // This callback is triggered when the local user joins the channel.
        public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
        {
            Debug.Log("You joined channel: " + connection.channelId);
        }

        public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
        {

            //check whether the current users joining is greater than the total allowed number of viewers
            if ((uid != _videoSample._channelId1) && (uid != _videoSample._channelId2))
            {

                for (int i = 0; i < _videoSample.num_viewers; i++)
                {

                    if (uid == _videoSample.remoteUid[i])
                    {

                        return;

                    }

                }

                //need to make a for loop that checks if the Views setEnabled is true

                for (int i = 0; i < _videoSample.num_viewers; i++)
                {

                    if (_videoSample.remoteUid[i] == 0)
                    {

                        // Setup remote view.
                        _videoSample.Views[i].SetForUser(uid, connection.channelId, VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
                        _videoSample.Views[i].SetEnable(true);
                        // Save the remote user ID in a variable.
                        _videoSample.remoteUid[i] = uid;
                        Debug.Log("You joined channel: " + connection + " : " + uid);
                        return;

                    }
                  

                }

            }

        }

    }

    internal class MediaPlayerObserver : IMediaPlayerSourceObserver
    {
        private readonly VideoAtlasClick _videoSample;

        internal MediaPlayerObserver(VideoAtlasClick videoSample)
        {
            _videoSample = videoSample;
        }
        public override void OnPlayerSourceStateChanged(MEDIA_PLAYER_STATE state, MEDIA_PLAYER_REASON reason)
        {

            int remote_surfaces_generated = 0;

            //loop through all local (video) students 
            for (int i = 0; i < _videoSample.num_total; i++)
            {
                //stops rendering videos
                if (remote_surfaces_generated >= _videoSample.num_viewers)
                {
                  
                    //check if video sample has media player
                    if (_videoSample.mediaPlayer[i] != null)
                    {
                        if (state == MEDIA_PLAYER_STATE.PLAYER_STATE_OPEN_COMPLETED)
                        {
                            // Media file opened successfully. Get the duration of file to setup the progress bar.
                            _videoSample.mediaPlayer[i].GetDuration(ref _videoSample.mediaDuration);
                            Debug.Log("File duration is : " + _videoSample.mediaDuration);
                            // Update the UI
                            _videoSample.mediaBtnText.text = "Play Media File";

                            //check if video sample has media player
                            if (_videoSample.mediaProgressBar[i] != null)
                            {
                                _videoSample.mediaProgressBar[i].value = 0;
                                _videoSample.mediaProgressBar[i].maxValue = _videoSample.mediaDuration / 1000;
                            }
                        }
                        else if (state == MEDIA_PLAYER_STATE.PLAYER_STATE_PLAYBACK_ALL_LOOPS_COMPLETED)
                        {
                            _videoSample.isMediaPlaying = false;
                            // Media file finished playing
                            _videoSample.mediaBtnText.text = "Load Media File";
                            // Restore camera and microphone streams
                            _videoSample.setupLocalVideo(false);
                            //_videoSample.updateChannelPublishOptions(false);
                            // Clean up
                            _videoSample.mediaPlayer[i].Dispose();
                            _videoSample.mediaPlayer = null;

                            //check if video sample has media player
                            if (_videoSample.mediaProgressBar[i] != null)
                            {
                                _videoSample.mediaProgressBar[i].value = 0;

                            }
                        }

                    }

                }
                else
                {

                    remote_surfaces_generated++;

                }

            }
        }
        public virtual void OnPositionChanged(Int64 position)
        {
            if (_videoSample.mediaDuration > 0)
            {
                int result = (int)((float)position / (float)_videoSample.mediaDuration * 100);


                int remote_surfaces_generated = 0;

                //loop through all local (video) students 
                for (int i = 0; i < _videoSample.num_total; i++)
                {
                    //Update the ProgressBar if a media player
                    if (remote_surfaces_generated >= _videoSample.num_viewers)
                    {
                        //check if video sample has media player
                        if (_videoSample.mediaProgressBar[i] != null)
                        {
                            _videoSample.mediaProgressBar[i].value = result;

                        }

                    }
                    else
                    {

                        remote_surfaces_generated++;

                    }
                }
            }
        }
        public override void OnPlayerEvent(MEDIA_PLAYER_EVENT eventCode, long elapsedTime, string message)
        {
            // Required to implement IMediaPlayerObserver
        }
        public override void OnMetaData(byte[] type, int length)
        {
            // Required to implement IMediaPlayerObserver
        }
    }

    internal class AudioFrameObserver : IAudioFrameObserver
    {
        private readonly VideoAtlasClick _videoSample;

        internal AudioFrameObserver(VideoAtlasClick videoSample)
        {
            _videoSample = videoSample;
        }

        // Sets whether to receives remote video data in multiple channels.
        public virtual bool IsMultipleChannelFrameWanted()
        {
            return true;
        }

        // Occurs each time the player receives an audio frame.
        public virtual bool OnFrame(AudioPcmFrame videoFrame)
        {
            return true;
        }

        // Retrieves the mixed captured and playback audio frame.
        public override bool OnMixedAudioFrame(string channelId, AudioFrame audioFrame)
        {
            return true;
        }

        // Gets the audio frame for playback.
        public override bool OnPlaybackAudioFrame(string channelId, AudioFrame audioFrame)
        {
            return true;
        }

        // Retrieves the audio frame of a specified user before mixing.
        public override bool OnPlaybackAudioFrameBeforeMixing(string channelId, uint uid, AudioFrame audioFrame)
        {
            return true;
        }

        // Gets the playback audio frame before mixing from multiple channels.
        public virtual bool OnPlaybackAudioFrameBeforeMixingEx(string channelId, uint uid, AudioFrame audioFrame)
        {
            return false;
        }

        // Gets the captured audio frame.
        public override bool OnRecordAudioFrame(string channelId, AudioFrame audioFrame)
        {
            return true;
        }
    }

    internal class VideoFrameObserver : IVideoFrameObserver
    {
        private readonly VideoAtlasClick _videoSample;

        internal VideoFrameObserver(VideoAtlasClick videoSample)
        {
            _videoSample = videoSample;
        }

        // Occurs each time the SDK receives a video frame before encoding.
        public virtual bool OnCaptureVideoFrame(VideoFrame videoFrame, VideoFrameBufferConfig config)
        {

            return true;
        }

        // Occurs each time the SDK receives a video frame before encoding.
        public virtual bool OnPreEncodeVideoFrame(VideoFrame videoFrame, VideoFrameBufferConfig config)
        {
            return true;
        }

        // Occurs each time the SDK receives a video frame sent by the remote user.
        public override bool OnRenderVideoFrame(string channelId, uint uid, VideoFrame videoFrame)
        {

            return true;
        }
    }

    void OnApplicationQuit()
    {
        if (RtcEngine != null)
        {
            Leave();
            RtcEngine.Dispose();
            RtcEngine = null;
        }

        int remote_surfaces_generated = 0;

        //loop through all local (video) students 
        for (int i = 0; i < num_total; i++)
        {
            if (remote_surfaces_generated >= num_viewers)
            {

                // Destroy the media player
                if (mediaPlayer[i] != null)
                {
                    mediaPlayer[i].Stop();

                }

            }
            else
            {

                remote_surfaces_generated++;

            }

        }
    }
}
