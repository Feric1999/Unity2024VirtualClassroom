Contains data files for Unity 2024 Virtual Classroom Project

room_data.txt

  - Line 1: App ID (_appID)
  
  - Line 2: Channel Name (_channelName)
  
  - Line 3: Token (_token)

  - Line 4: UserID (_userID)
    
  - Line 5: User Token (_userToken)
  

rows_data.txt

  - Line 1:  Number of remote students connecting through agora (num_viewers)
  
  - Line 2:  Total number of remote students connecting through agora and inbeded videos. Note must be divisible by num_rows (num_total)
  
  - Line 3:  Number of rows in video atlas (num_rows)
  
  - Line 4:  Number of wireframes in the virtual classroom to be used (num_wireframe)

  - Line 5: Number of local students (num_local)


name_list.txt

  - Used to hold a number of names to be applied to users in the game. Must be equal to the number of remote students connecting through agora (num_viewers)

calibrate_canvas.txt

  - used to hold orientation of tables in remote student projection
    
  - Line 1: Position x
    
  - Line 2: Position y
    
  - Line 3: Position z
    
  - Line 4: Rotation x
    
  - Line 5: Rotation x
    
  - Line 6: Rotation x
  


Program reserve UIDs

  - Instructor Camera UID: 100

  - Video Atlas UID: 99

  - Slides UID: 101
    
  - Local Student Camera UID: 200-300

  - Remote Student Background UID: 199

IP Address
  - holds IP address of server for all local student sign in apps
  - holds port of local student sign in apps
  
