# Avatar Logger

Simple Collect avatars from log files and uploadthem to verius data base providers 

## Instructions
Simply hrab a release and run it its that simple it will run in the background and log avatars.



## Instructions to add your database

1. Add the upload URL to the database to the configuration settings in `CommonFuncs.cs`
2. Configure the database to accept avatar IDs in array format.
   IE
```json
["id1", "id2", "id3"]
```
