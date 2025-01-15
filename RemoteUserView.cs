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

public class RemoteUserView : MonoBehaviour
{
    //address of Agora room info
    public string room_info_address = "https://raw.githubusercontent.com/Feric1999/data/main/room_data.txt";

    // Fill in your app ID.
    internal string _appID = "745561d6795943978f25fe00b177b80f";
    // Fill in your channel name.
    internal string _channelName = "Oculus Test";
    // Fill in the temporary token you obtained from Agora Console.
    internal string _token = "";
  
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

    //internal VideoSurface LocalView
    internal IRtcEngine RtcEngine;

    //VideoSurface used to hold instructor
    private VideoSurface RemoteView;
    //VideoSurface used to hold local user view
    private VideoSurface LocalView;

    //variabels used to control background subtraction
    bool isVirtualBackGroundEnabled = false;
    int counter = 0;

    //Modify Camera
    public Camera curr_camera;

    //channel ID for instructor webcam
    public uint _channelId2 = 100;

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    private ArrayList permissionList = new ArrayList() { Permission.Camera, Permission.Microphone };
#endif

    // Start is called before the first frame update
    void Start()
    {

        //File.Create(Application.persistentDataPath + "//test.txt");
        SetupVideoSDKEngine();
        InitEventHandler();
        SetupUI();
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

    public void setVirtualBackground()
    {

        counter++;

        if (counter > 2)
        {
            counter = 0;
            isVirtualBackGroundEnabled = false;
            Debug.Log("Virtual background turned off");
        }
        else
        {
            isVirtualBackGroundEnabled = true;
        }
        VirtualBackgroundSource virtualBackgroundSource = new VirtualBackgroundSource();

        // Set the type of virtual background
        if (counter == 1)
        { // Set background blur
            virtualBackgroundSource.background_source_type = BACKGROUND_SOURCE_TYPE.BACKGROUND_BLUR;
            virtualBackgroundSource.blur_degree = BACKGROUND_BLUR_DEGREE.BLUR_DEGREE_HIGH;
            Debug.Log("Blur background enabled");
        }
        else if (counter == 2)
        { // Set a solid background color
            virtualBackgroundSource.background_source_type = BACKGROUND_SOURCE_TYPE.BACKGROUND_COLOR;
            virtualBackgroundSource.color = 0xFFFFFF;
            Debug.Log("Color background enabled");
        }

        // Set processing properties for background
        SegmentationProperty segmentationProperty = new SegmentationProperty();
        segmentationProperty.modelType = SEG_MODEL_TYPE.SEG_MODEL_AI; // Use SEG_MODEL_GREEN if you have a green background
        segmentationProperty.greenCapacity = 0.5F; // Accuracy for identifying green colors (range 0-1)

        // Enable or disable virtual background
        int x = RtcEngine.EnableVirtualBackground(
            isVirtualBackGroundEnabled,
            virtualBackgroundSource, segmentationProperty);

        Debug.Log(x);
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


        //flip surfaces so images not backward

        GameObject go = GameObject.Find("RemoteView");
        RemoteView = go.AddComponent<VideoSurface>();
        go.transform.Rotate(0.0f, 0.0f, -180.0f);

        go = GameObject.Find("LocalView");
        LocalView = go.AddComponent<VideoSurface>();
        go.transform.Rotate(0.0f, 0.0f, -180.0f);

    }

    private void PreviewSelf()
    {
        // Enable the video module
        RtcEngine.EnableVideo();
        // Enable local video preview
        RtcEngine.StartPreview();
        // Set up local video display
        LocalView.SetForUser(0, "");
        // Render the video
        LocalView.SetEnable(true);
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

        go = GameObject.Find("virtualBackground");
        go.GetComponent<Button>().onClick.AddListener(setVirtualBackground);


    }

    //function used to Initialize an IRtcEngine instance
    private void SetupVideoSDKEngine()
    {
        // Create an IRtcEngine instance
        RtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();

        RtcEngineContext context = new RtcEngineContext();
        context.appId = _appID;
        context.channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING;
        context.audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT;
        // Initialize the instance
        RtcEngine.Initialize(context);
    }

    private void InitEventHandler()
    {
        // Creates a UserEventHandler instance.
        UserEventHandler handler = new UserEventHandler(this);
        RtcEngine.InitEventHandler(handler);

    }

    //helper function used to join a room. Attached to the "Join" button
    public void Join()
    {

        //start video recording of user
        PreviewSelf();

        //sets the video encoder options which control frame and bitrate
        SetVideoEncoderConfiguration();

        // Set channel media options
        ChannelMediaOptions options = new ChannelMediaOptions();

        // Publish the audio stream collected from the microphone
        options.publishMicrophoneTrack.SetValue(true);
        // Publish the video stream collected from the camera
        options.publishCameraTrack.SetValue(true);
        // Automatically subscribe to all audio streams
        options.autoSubscribeAudio.SetValue(true);
        // Automatically subscribe to all video streams
        options.autoSubscribeVideo.SetValue(true);
        // Set the channel profile to live broadcasting
        options.channelProfile.SetValue(CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING);
        // Set the user role to broadcaster
        options.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);
        // Join the channel
        RtcEngine.JoinChannel(_token, _channelName, 0, options);


    }
    //helper function used to join a room. Attached to the "Leave" button
    public void Leave()
    {
        Debug.Log("Leaving " + _channelName);

        // Disable video module
        RtcEngine.StopPreview();
        // Leave the channel
        RtcEngine.LeaveChannel();
        // Disable the video module
        RtcEngine.DisableVideo();
        // Stop remote video rendering
        RemoteView.SetEnable(false);
        // Stop local video
        LocalView.SetEnable(false);

    }

    public void SetVideoEncoderConfiguration()
    {
        VideoEncoderConfiguration config = new VideoEncoderConfiguration();
        // Sets the video resolution.
        config.dimensions.width = 480;
        config.dimensions.height = 360;

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
        private readonly RemoteUserView _videoSample;

        internal UserEventHandler(RemoteUserView videoSample)
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
            if (uid == _videoSample._channelId2)
            {
                _videoSample.RemoteView.SetEnable(false);
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
            if (uid == _videoSample._channelId2)
            {

                // Setup remote view.
                _videoSample.RemoteView.SetForUser(uid, connection.channelId, VIDEO_SOURCE_TYPE.VIDEO_SOURCE_REMOTE);
                _videoSample.RemoteView.SetEnable(true);
                Debug.Log("You joined channel: " + connection.channelId);

            }

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

    }
}
