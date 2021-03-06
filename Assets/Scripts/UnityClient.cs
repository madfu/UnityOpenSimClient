using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Rendering;
using OpenMetaverse.Imaging;
using System.IO;
using System.Drawing;
using System;

public class UnityClient : MonoBehaviour {
	string msg = "";	
	public UnityEngine.Material m_defaultTerrainMat;
	public UnityEngine.Material m_defaultObjectMat;
	public GUISkin m_GUISkin;
	
	string m_userName = "qiang cao";
	string m_password = "1234";
	string m_server = "http://192.168.1.134:9000";
	
	GridClient Client = new GridClient();
	
	LogInOutProcess m_loginout;
	TerrainProcess m_terrain;
	TextureProcess m_textures;
	ObjectProcess m_objects;
	CameraProcess m_camera;
	SelfMoveProcess m_selfmove;
	AvatarsProcess m_avatars;
	
	// Use this for initialization
	void Start () {
		Client.Settings.ASSET_CACHE_DIR = Utility.GetOpenMetaverseCacheDir();
		Settings.RESOURCE_DIR = Utility.GetResourceDir();
		calcGuiParams();
		Utility.InstallCharacterAndOmvResource();

		if (Debug.isDebugBuild)
			msg += "persistentData: " + Application.persistentDataPath + "\n" 
	            + "data: " + Application.dataPath + "\n" 
	            + "cache: " + Application.temporaryCachePath + "\n" 
				+ "stream: " + Application.streamingAssetsPath + "\n" 
	            + "curDir: " + System.IO.Directory.GetCurrentDirectory();
		
		m_loginout = new LogInOutProcess(Client);
		m_terrain = new TerrainProcess(Client, m_defaultTerrainMat);
		m_textures = new TextureProcess(Client, m_defaultObjectMat);
		m_objects = new ObjectProcess(Client, m_textures, m_defaultObjectMat);
		m_camera = new CameraProcess(Client);
		m_selfmove = new SelfMoveProcess(Client);
		m_avatars = new AvatarsProcess(Client, m_textures);
	}
	
	bool logoutRequesting = false;
	// Update is called once per frame
	void Update () {
		if (Client.Network.Connected && logoutRequesting == false)
		{						
			m_terrain.Update();
			m_objects.Update();
			m_camera.Update();
			m_selfmove.Update();
			m_avatars.Update();
		}
		else if (!Client.Network.Connected && logoutRequesting == true)
			logoutRequesting = false;
		
		if (m_loginout == null)
			Debug.Log("m_loginout == null");
	}
	
	void calcGuiParams()
	{
		b1y = (int)(Screen.height * 0.7);
		l3y = b1y - h - gap;
		l1y = l3y - h - gap;
		
		l2x = Screen.width / 2;
		l1x = l3x = l2x - w - w;
		t1x = t3x = l1x + w;
		t2x = l2x + w;
		b1x = l2x - w / 2;
		
		b2x = Screen.width - 2 * w;
		b2y = Screen.height - 2 * h;
		
		l2x += gap;
	}
	
	int l1x, t1x, l2x, t2x, l3x, t3x, b1x, b2x;
	int l1y, l3y, b1y, b2y;
	int h = 50, w = 100, gap = 10;
	void OnGUI()
	{
		GUI.skin = m_GUISkin;
		if (Client.Network.Connected == false)
		{
			GUI.Label(new Rect(l1x, l1y, w, h), "用户名");
			m_userName = GUI.TextField(new Rect(t1x, l1y, w, h), m_userName);
			GUI.Label(new Rect(l2x, l1y, w, h), "密码");
			m_password = GUI.TextField(new Rect(t2x, l1y, w, h), m_password);
			GUI.Label(new Rect(l3x, l3y, w, h), "登录地址");
			m_server = GUI.TextField(new Rect(t3x, l3y, 3*w, h), m_server);
			if (GUI.Button(new Rect(b1x, b1y, w, h), "登录"))
				m_loginout.StartLogin(m_userName, m_password, m_server);
			if (Debug.isDebugBuild) 
				GUILayout.Label(msg);
		}
		else
		{
			if (GUI.Button(new Rect(b2x, b2y, w, h), "登出"))
			{
				logoutRequesting = true;
				ClearScene();
			}
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			ClearScene();
			Application.Quit();
		}
	}
	
	void ClearScene()
	{		
		m_loginout.Clear();
		m_terrain.Clear();
		m_objects.Clear();
		m_camera.Clear();
		m_avatars.Clear();
	}
}

public class LogInOutProcess
{
	GridClient Client;
	
	public LogInOutProcess(GridClient client)
	{
		Client = client;
		Client.Network.LoginProgress += Network_OnLoginProcess;
	}
	
	void Network_OnLoginProcess(object sender, LoginProgressEventArgs e)
    {
        if (e.Status == LoginStatus.ConnectingToSim)
        { // first time
			Debug.Log("Connecting to OpenSim");
        }
        else if (e.Status == LoginStatus.Success)
        { // second time
            Debug.Log("Connecting Success!");
        }
        else if (e.Status == LoginStatus.Failed)
        {
            Debug.Log("Connecting Failed!");
        }
    }
	
	public void StartLogin(string name, string pass, string server)
	{
		string LOGIN_SERVER = server;
        string FIRST_NAME = name.Split(' ')[0];
        string LAST_NAME = name.Split(' ')[1];
        string PASSWORD = pass;
        string CHANNEL = "UnityChannel";
        string VERSION = "0.0.1";

        LoginParams loginParams = Client.Network.DefaultLoginParams(
            FIRST_NAME, LAST_NAME, PASSWORD, CHANNEL, VERSION);
        loginParams.URI = LOGIN_SERVER; 
		loginParams.Start = "last";
		loginParams.AgreeToTos = true;
        // Login to Simulator
        Client.Network.BeginLogin(loginParams);
		Debug.Log("Start Connecting");
	}
	
	public void LogOut()
	{
		if (Client.Network.Connected)
		{
			Client.Network.Logout();
			Debug.Log ("Logout success!");
		}
	}
	
	public void Clear()
	{
		LogOut();
	}
}

public class TerrainProcess
{
	float[,] heightTable = new float[256, 256];
	System.Drawing.Bitmap terrainImage = null;
	
	GameObject terrain;
	GameObject terrainPart1;
	GameObject terrainPart2;
	UnityEngine.Mesh mesh1;
	UnityEngine.Mesh mesh2;
	UnityEngine.Material defaultMat;
	
	bool terrainModified = false;
	const float MinimumTimeBetweenTerrainUpdated = 4f;	//in second
	float terrainTimeSinceUpdate = MinimumTimeBetweenTerrainUpdated + 1f;
		
	GridClient Client;
	UnityEngine.Material m_defaultTerrainMat;	
	
	public TerrainProcess(GridClient client, UnityEngine.Material terrainMat)
	{		
		Client = client;
		m_defaultTerrainMat = terrainMat;
		
		terrain = new GameObject("terrain");
		terrain.transform.position = new UnityEngine.Vector3(0, 0, 0);
		
		terrainPart1 = new GameObject("part1");
		terrainPart1.transform.parent = terrain.transform;
		terrainPart1.transform.localPosition = new UnityEngine.Vector3(0, 0, 0);
		terrainPart1.AddComponent("MeshRenderer");
		terrainPart1.AddComponent("MeshFilter");
		mesh1 = (terrainPart1.GetComponent<MeshFilter>() as MeshFilter).mesh;
		
		terrainPart2 = new GameObject("part2");
		terrainPart2.transform.parent = terrain.transform;
		terrainPart2.transform.localPosition = new UnityEngine.Vector3(0, 0, 0);
		terrainPart2.AddComponent("MeshRenderer");
		terrainPart2.AddComponent("MeshFilter");
		mesh2 = (terrainPart2.GetComponent<MeshFilter>() as MeshFilter).mesh;
		
		Client.Settings.STORE_LAND_PATCHES = true;
		Client.Terrain.LandPatchReceived += Terrain_LandPatchReceived;
		
		if (Application.platform == RuntimePlatform.Android)
		{
			terrainPart1.GetComponent<MeshRenderer>().material = m_defaultTerrainMat;
			terrainPart2.GetComponent<MeshRenderer>().material = m_defaultTerrainMat;
		}
	}
	
	void Terrain_LandPatchReceived(object sender, LandPatchReceivedEventArgs e)
	{
		//leave other regions out temporarily
		if (e.Simulator.Handle != Client.Network.CurrentSim.Handle) return;
		//Debug.Log ("LandPatchReceive");
		terrainModified = true;
	}
	
	public void Update()
	{
		terrainTimeSinceUpdate += Time.deltaTime;
		if (terrainModified && terrainTimeSinceUpdate > MinimumTimeBetweenTerrainUpdated)
		{
			UpdateTerrain();
		  	UpdateTerrainTexture();
		}
	}
	
	void UpdateTerrain()
    {
		//We have to change terrainModified at very first in case of new LandPatch received during UpdateTerrain
		terrainModified = false;
		
        if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Terrain == null) return;
		//Debug.Log("UpdateTerrain");
        int step = 1;

        for (int x = 0; x < 256; x += step)
        {
            for (int y = 0; y < 256; y += step)
            {
                float z = 0;
                int patchNr = ((int)x / 16) * 16 + (int)y / 16;
                if (Client.Network.CurrentSim.Terrain[patchNr] != null
                    && Client.Network.CurrentSim.Terrain[patchNr].Data != null)
                {
                    float[] data = Client.Network.CurrentSim.Terrain[patchNr].Data;
                    z = data[(int)x % 16 * 16 + (int)y % 16];
                }
                heightTable[x, y] = z;	
            }
        }

        Face face = Utility.R.TerrainMesh(heightTable, 0f, 255f, 0f, 255f);
		
		//Unity doesn't allow a mesh with over 65000 vertices while face have 65536 vertices.
		//We need to split face mesh into 2 peices.
		//mesh1: vertices 0~32768+255=33023, normals 0~33023, uv 0~33023, indices 0~255*2*128*3-1=195839
		//mesh2: vertices 32768~65535, normals 32768~65535, uv 32768~65535, indices 195840~390149
		
		Utility.MakeMesh(mesh1, face, 0, 33023, 0, 195839, 0);
		Utility.MakeMesh(mesh2, face, 32768, 65535, 195840, 390149, 32768);
		
        terrainTimeSinceUpdate = 0f;
        //if (terrainModified)Debug.Log("terrainModified set to true by other thread!");
    }
	
	void UpdateTerrainTexture()
	{
		if (Application.platform == RuntimePlatform.WindowsPlayer 
			|| Application.platform == RuntimePlatform.WindowsEditor)
		{
			Simulator sim = Client.Network.CurrentSim;
	        terrainImage = Radegast.Rendering.TerrainSplat.Splat(Client, heightTable,
	            new UUID[] { sim.TerrainDetail0, sim.TerrainDetail1, sim.TerrainDetail2, sim.TerrainDetail3 },
	            new float[] { sim.TerrainStartHeight00, sim.TerrainStartHeight01, sim.TerrainStartHeight10, sim.TerrainStartHeight11 },
	            new float[] { sim.TerrainHeightRange00, sim.TerrainHeightRange01, sim.TerrainHeightRange10, sim.TerrainHeightRange11 });
			
			Texture2D tex = Utility.Bitmap2Texture2D(terrainImage);
			terrainPart1.GetComponent<MeshRenderer>().material.mainTexture = tex;
			terrainPart2.GetComponent<MeshRenderer>().material.mainTexture = tex;
		}
		
		//sadly on android we don't have libgdiplus.so, so we can't use some functions in System.Drawing.dll, so I can't update terrain texture now 
	}
	
	public void Clear()
	{
		mesh1.Clear();
		mesh2.Clear();
	}
}

public class TextureProcess
{
	GridClient Client;
	Dictionary<UUID, UnityEngine.Material> materials = new Dictionary<UUID, UnityEngine.Material>();
	Dictionary<UUID, Texture2D> textures = new Dictionary<UUID, Texture2D>();
	Dictionary<UUID, Bitmap> bitmaps = new Dictionary<UUID, Bitmap>();
	UnityEngine.Material m_defaultMat;
	
	public TextureProcess(GridClient client, UnityEngine.Material mat)
	{
		Client = client;
		m_defaultMat = mat;
	}
	
	Bitmap Jpgbytes2Bitmap(byte[] jpg)
	{
		ManagedImage mi;
		if (!OpenJPEG.DecodeToImage(jpg, out mi)) return null;
		byte[] imageBytes = mi.ExportTGA();
		
		using (MemoryStream byteData = new MemoryStream(imageBytes))
		{
			return LoadTGAClass.LoadTGA(byteData);
		}	
	}
	
	public void DownloadTexture(UUID textureID)
	{
		//return;
		if (!textures.ContainsKey(textureID) && !bitmaps.ContainsKey(textureID))
		{
			if (Client.Assets.Cache.HasAsset(textureID))
			{
				//Debug.Log("Cache hits!");
				byte[] jpg = Client.Assets.Cache.GetCachedAssetBytes(textureID);
				
				Bitmap img = Jpgbytes2Bitmap(jpg);
				if (img == null) return;
				bitmaps[textureID] = img;	//fixme: there may be access violation
			}
			else
			{
				TextureDownloadCallback handler = (state, asset) =>
				{
					//Debug.Log("state is " + state.ToString());
					try{
	                	switch (state)
	                    {
	                    	case TextureRequestState.Finished:
							{
								Bitmap img = Jpgbytes2Bitmap(asset.AssetData);
								if (img == null) return;
								bitmaps[textureID] = img;	//fixme: there may be access violation
	                    		break;
							}                              	
	   						case TextureRequestState.Aborted:
	                    	case TextureRequestState.NotFound:
	                    	case TextureRequestState.Timeout:
	                    		break;
	                    }
					}
					catch(Exception ex)
					{
						Debug.Log("what happened?:" + ex.Message);
					}
                };
				
				Client.Assets.RequestImage(textureID, ImageType.Normal, handler);
			}
		}
	}
	
	public Texture2D GetTexture2D(UUID texUUID)
	{
		if (textures.ContainsKey(texUUID))
			return textures[texUUID];
		else if (bitmaps.ContainsKey(texUUID))
		{
			Texture2D tex = Utility.Bitmap2Texture2D(bitmaps[texUUID]);
			tex.wrapMode = TextureWrapMode.Repeat;
			textures[texUUID] = tex;
			bitmaps.Remove(texUUID);
			return tex;
		}
		return null;
	}
	
	public UnityEngine.Material GetMaterial(UUID texUUID)
	{
		if (materials.ContainsKey(texUUID))
			return materials[texUUID];
			
		Texture2D tex = GetTexture2D(texUUID);
		if (tex != null)
		{
			UnityEngine.Material mat = new UnityEngine.Material(m_defaultMat);
			mat.mainTexture = tex;
			materials[texUUID] = mat;
			textures.Remove(texUUID);
			return mat;
		}
		return null;
	}
}

public class ObjectProcess
{
	GridClient Client;
	TextureProcess m_textures;
	Dictionary<uint, FacetedMesh> newPrims = new Dictionary<uint, FacetedMesh>();
	Dictionary<uint, GameObject> objects = new Dictionary<uint, GameObject>();
	GameObject primObjects = new GameObject("prims");
	UnityEngine.Material m_defaultObjectMat;
	
	public ObjectProcess(GridClient client, TextureProcess textureProcess, UnityEngine.Material defaultObjectMat)
	{
		Client = client;
		m_textures = textureProcess;
		m_defaultObjectMat = defaultObjectMat;
		Client.Objects.ObjectUpdate += Objects_OnObjectUpdate;
	}
	
	void Objects_OnObjectUpdate(object sender, PrimEventArgs e)
    {
		//leave other regions out temporarily
		if (e.Simulator.Handle != Client.Network.CurrentSim.Handle) return;
		
		//leave tree out temporarily. Radegast doesn't implement tree rendering yet.
		if (e.Prim.PrimData.PCode != PCode.Prim)
		{
			//Debug.Log("Receive " + e.Prim.PrimData.PCode.ToString());
			return;
		}		
		
		//FIXME : need to update prims?
		if (objects.ContainsKey(e.Prim.LocalID))
		{
			//Debug.Log ("receive prim with LocalID " + e.Prim.LocalID.ToString() + " again!");
			return;
		}
				
		if (e.Prim.Sculpt != null)
		{
			//leave sculpt prim out temporarily
		}
		else
		{
			FacetedMesh mesh = Utility.R.GenerateFacetedMesh(e.Prim, DetailLevel.Highest);
			lock (newPrims)
			{
				newPrims[e.Prim.LocalID] = mesh;
			}
			if (Application.platform == RuntimePlatform.WindowsPlayer 
				|| Application.platform == RuntimePlatform.WindowsEditor)
			{
				foreach (Face face in mesh.Faces)
					m_textures.DownloadTexture(face.TextureFace.TextureID);
			}             
		}
	}
	
	public void Update()
	{
		lock (newPrims)
		{
			foreach (var item in newPrims)
			{			
				ProcessPrim(item.Value);
			}
			newPrims.Clear();
		}
	}
	
	void ProcessPrim(FacetedMesh mesh)
	{
		if (objects.ContainsKey(mesh.Prim.LocalID))
			return;
			
		GameObject parent = null;
		if (mesh.Prim.ParentID != 0)
		{
			if (!objects.ContainsKey(mesh.Prim.ParentID))
			{
				if (newPrims.ContainsKey(mesh.Prim.ParentID) == false)
					return;//temperarily ignore the prim
				ProcessPrim(newPrims[mesh.Prim.ParentID]);	
			}
			parent = objects[mesh.Prim.ParentID];			
		}
		else
			parent = primObjects;
		
		GameObject obj = new GameObject(mesh.Prim.LocalID.ToString());
		
		// Create vertices, uv, triangles for EACH FACE that stores the 3D data in Unity3D friendly format
		for (int j = 0; j < mesh.Faces.Count; j++)
	    {
			Face face = mesh.Faces[j];
			GameObject faceObj = new GameObject("face" + j.ToString());
			faceObj.transform.parent = obj.transform;
				
			MeshRenderer mr = (faceObj.AddComponent("MeshRenderer") as MeshRenderer);
			if (Application.platform == RuntimePlatform.WindowsPlayer 
				|| Application.platform == RuntimePlatform.WindowsEditor)
			{				
				UnityEngine.Material mat = m_textures.GetMaterial(face.TextureFace.TextureID);
				//Texture2D tex = m_textures.GetTexture2D(face.TextureFace.TextureID);
				if (mat != null)
					mr.material = mat;
				else
					mr.material = m_defaultObjectMat;
			}
			else
				mr.material = m_defaultObjectMat;
			
			UnityEngine.Mesh unityMesh = (faceObj.AddComponent("MeshFilter") as MeshFilter).mesh;
			
			Utility.MakeMesh(unityMesh, face, 0, face.Vertices.Count - 1, 0, face.Indices.Count - 1, 0);
		}			
		//second life's child object's position and rotation is local, but scale are global. 
		//So we have to set parent when setting position, and unset parent when setting rotation and scale.
		//Radegast explains well:
		//pos = parentPos + obj.InterpolatedPosition * parentRot;
        //rot = parentRot * obj.InterpolatedRotation;
		obj.transform.position = parent.transform.position + 
			parent.transform.rotation * new UnityEngine.Vector3(mesh.Prim.Position.X, mesh.Prim.Position.Y, -mesh.Prim.Position.Z);
				
		//we invert the z axis, and Second Life rotatation is about right hand, but Unity rotation is about left hand, so we negate the x and y part of the quaternion. 
		//You have to deeply understand the quaternion to understand this.
		obj.transform.rotation = parent.transform.rotation * new UnityEngine.Quaternion(-mesh.Prim.Rotation.X, -mesh.Prim.Rotation.Y, mesh.Prim.Rotation.Z, mesh.Prim.Rotation.W);
		obj.transform.localScale = new UnityEngine.Vector3(mesh.Prim.Scale.X, mesh.Prim.Scale.Y, mesh.Prim.Scale.Z);
		objects[mesh.Prim.LocalID] = obj;
		obj.transform.parent = primObjects.transform;
		//Debug.Log("prim " + mesh.Prim.LocalID.ToString() + ": Pos,"+mesh.Prim.Position.ToString() + " Rot,"+mesh.Prim.Rotation.ToString() + " Scale,"+mesh.Prim.Scale.ToString());
		//Sadly, when it comes to non-uniform scale parent, Unity will skew the child, so we cannot make hierachy of the objects.
	}
	
	public void Clear()
	{
		foreach (var item in objects)
			UnityEngine.Object.Destroy(item.Value);
		objects.Clear();
		newPrims.Clear();
	}
}

public class CameraProcess
{
	GridClient Client;
	bool setClearFlags = false;
	
	public CameraProcess(GridClient client)
	{
		Client = client;
	}
	
	public void Update()
	{
		if (setClearFlags == false)
		{
			Camera.main.clearFlags = CameraClearFlags.Skybox;
			setClearFlags = true;
		}
		
		OpenMetaverse.Vector3 camPos = Client.Self.SimPosition +
			new OpenMetaverse.Vector3(-4, 0, 1) * Client.Self.Movement.BodyRotation;
		Camera.main.transform.position = new UnityEngine.Vector3(camPos.X, camPos.Y, -camPos.Z);	//watch out the negated z
		
		OpenMetaverse.Vector3 focalPos = Client.Self.SimPosition +
			new OpenMetaverse.Vector3(5, 0, 0) * Client.Self.Movement.BodyRotation;
		Camera.main.transform.LookAt(new UnityEngine.Vector3(focalPos.X, focalPos.Y, -focalPos.Z), UnityEngine.Vector3.back);//watch out the negated z
	}
	
	public void Clear()
	{
		Camera.main.clearFlags = CameraClearFlags.SolidColor;
		setClearFlags = false;
	}
}

public class SelfMoveProcess
{
	GridClient Client;
	bool isHoldingHome = false;
	float upKeyHeld = 0;
	const float upKeyHeldBeforeFly = 0.5f;
	
	public SelfMoveProcess(GridClient client)
	{
		Client = client;
	}
	
	public void Update()
	{
		float time = Time.deltaTime;
		
		if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
		{
			Client.Self.Movement.AtPos = Input.GetAxisRaw("Vertical") > 0;
			Client.Self.Movement.AtNeg = Input.GetAxisRaw("Vertical") < 0;
			Client.Self.Movement.TurnLeft = Input.GetAxisRaw("Horizontal") < 0;
			Client.Self.Movement.TurnRight = Input.GetAxisRaw("Horizontal") > 0;
			if (Client.Self.Movement.TurnLeft)
				Client.Self.Movement.BodyRotation *= OpenMetaverse.Quaternion.CreateFromAxisAngle(OpenMetaverse.Vector3.UnitZ, time);
			else if (Client.Self.Movement.TurnRight)
				Client.Self.Movement.BodyRotation *= OpenMetaverse.Quaternion.CreateFromAxisAngle(OpenMetaverse.Vector3.UnitZ, -time);
			Client.Self.Movement.UpPos = Input.GetAxisRaw("Jump") > 0;
			Client.Self.Movement.UpNeg = Input.GetAxisRaw("Jump") < 0;
			
			if (Input.GetAxisRaw("Fly") > 0)
			{
				//Holding the home key only makes it change once, 
	            // not flip over and over, so keep track of it
				if (isHoldingHome == false)
				{
					Client.Self.Movement.Fly = !Client.Self.Movement.Fly;
					isHoldingHome = true;
				}
			}
			else
				isHoldingHome = false;	
		}
		else if (Application.platform == RuntimePlatform.Android)
		{
			if (Input.touchCount == 1)
			{
				Touch t = Input.GetTouch(0);
				UnityEngine.Vector2 dp = t.deltaPosition;
				float xAbs = Math.Abs(dp.x);
				float yAbs = Math.Abs(dp.y);
				if (xAbs == 0 && yAbs == 0)
					return;
				if (xAbs > yAbs)
				{
					Debug.Log("I am turning!!");
					if (dp.x > 0)
					{
						//turn right
						Client.Self.Movement.TurnLeft = false;
						Client.Self.Movement.TurnRight = true;
						Client.Self.Movement.BodyRotation *= OpenMetaverse.Quaternion.CreateFromAxisAngle(OpenMetaverse.Vector3.UnitZ, -time);
					}
					else
					{
						//turn left
						Client.Self.Movement.TurnLeft = true;
						Client.Self.Movement.TurnRight = false;
						Client.Self.Movement.BodyRotation *= OpenMetaverse.Quaternion.CreateFromAxisAngle(OpenMetaverse.Vector3.UnitZ, time);
					}					
					Client.Self.Movement.AtPos = false;
					Client.Self.Movement.AtNeg = false;
				}
				else
				{
					if (dp.y > 0)
					{
						//forword
						Client.Self.Movement.AtPos = true;
						Client.Self.Movement.AtNeg = false;

					}
					else
					{
						//backward
						Client.Self.Movement.AtPos = false;
						Client.Self.Movement.AtNeg = true;
					}
					Client.Self.Movement.TurnLeft = false;
					Client.Self.Movement.TurnRight = false;
				}	
				Client.Self.Movement.UpPos = false;
				Client.Self.Movement.UpNeg = false;
			}
			else if (Input.touchCount == 2)
			{
				//fly up
				Client.Self.Movement.AtPos = false;
				Client.Self.Movement.AtNeg = false;
				Client.Self.Movement.TurnLeft = false;
				Client.Self.Movement.TurnRight = false;
				Client.Self.Movement.UpPos = true;
				Client.Self.Movement.UpNeg = false;
				
				
			}
			else if (Input.touchCount == 3)
			{
				//fly down
				Client.Self.Movement.AtPos = false;
				Client.Self.Movement.AtNeg = false;
				Client.Self.Movement.TurnLeft = false;
				Client.Self.Movement.TurnRight = false;
				Client.Self.Movement.UpPos = false;
				Client.Self.Movement.UpNeg = true;
			}
			else
			{
				Client.Self.Movement.AtPos = false;
				Client.Self.Movement.AtNeg = false;
				Client.Self.Movement.TurnLeft = false;
				Client.Self.Movement.TurnRight = false;
				Client.Self.Movement.UpPos = false;
				Client.Self.Movement.UpNeg = false;
			}
		}
		
        if (!Client.Self.Movement.Fly &&
            Client.Self.Movement.UpPos)
        {
            upKeyHeld += time;
            if (upKeyHeld > upKeyHeldBeforeFly)//Wait for a bit before we fly, they may be trying to jump
                Client.Self.Movement.Fly = true;
        }
        else
            upKeyHeld = 0;//Reset the count
		
		if (Client.Self.Velocity.Z > 0 && Client.Self.Movement.UpNeg)//HACK: Sometimes, stop fly fails
        	Client.Self.Fly(false);//We've hit something, stop flying
	}
}

public class AvatarsProcess
{
	GridClient Client;
	TextureProcess m_textures;
	GameObject avatarObjects;
	Dictionary<uint, Radegast.Rendering.RenderAvatar> newAvatars = new Dictionary<uint, Radegast.Rendering.RenderAvatar>();
	Dictionary<uint, GameObject> avatars = new Dictionary<uint, GameObject>();
	Dictionary<uint, Radegast.Rendering.RenderAvatar> renderAvatars = new Dictionary<uint, Radegast.Rendering.RenderAvatar>();
	List<uint> avHasTex = new List<uint>();
	Dictionary<AvatarTextureIndex, UnityEngine.Material> avMaterials = new Dictionary<AvatarTextureIndex, UnityEngine.Material>();
	const string avatarNum = "female/6/";
	
	public AvatarsProcess(GridClient client, TextureProcess textureProcess)
	{
		Client = client;
		m_textures = textureProcess;
		avatarObjects = new GameObject("avatars");
		Radegast.Rendering.GLAvatar.loadlindenmeshes2("avatar_lad.xml");

		//Receive all the avatars' models
		Client.Objects.AvatarUpdate += Objects_AvatarUpdate;
		//Receive other avatars' textures		
		Client.Avatars.AvatarAppearance += Avatars_AvatarAppearance;
		//Receive self's textures 1st login time
		Client.Appearance.AppearanceSet += Appearance_AppearanceSet;
		
		if (Application.platform == RuntimePlatform.Android)
		{
			buildAvMats(avatarNum);
		}
	}
	
	void buildAvMats(string num)
	{
		UnityEngine.Material mat;
		mat = Resources.Load(num + "lowerBody") as UnityEngine.Material;
		avMaterials[AvatarTextureIndex.LowerBaked] = mat;
		mat = Resources.Load(num + "upperBody") as UnityEngine.Material;
		avMaterials[AvatarTextureIndex.UpperBaked] = mat;
		mat = Resources.Load(num + "face") as UnityEngine.Material;
		avMaterials[AvatarTextureIndex.HeadBaked] = mat;
		mat = Resources.Load("eyeBall") as UnityEngine.Material;
		avMaterials[AvatarTextureIndex.EyesBaked] = mat;
		mat = Resources.Load("hair") as UnityEngine.Material;
		avMaterials[AvatarTextureIndex.HairBaked] = mat;
	}
		
	//Receive all the avatars' models
	void Objects_AvatarUpdate(object sender, AvatarUpdateEventArgs e)
	{
		//leave other regions out temporarily
		if (e.Simulator.Handle != Client.Network.CurrentSim.Handle) return;
		
		Radegast.Rendering.GLAvatar ga = new Radegast.Rendering.GLAvatar();
		OpenMetaverse.Avatar av = e.Avatar;
                    
        Radegast.Rendering.RenderAvatar ra = new Radegast.Rendering.RenderAvatar();
        ra.avatar = av;
        ra.glavatar = ga;

        newAvatars.Add(av.LocalID, ra);
        ra.glavatar.morph(av);
	}
	
	public void Update()
	{
		foreach (var item in newAvatars)
		{
			ProcessAvatar(item.Value);
		}
		newAvatars.Clear();
		MoveAvatars();		//Update avatars' coordinates
		
		//Update avatar textures
		if (avHasTex.Count > 0)
			UpdateAvTexture(avHasTex[0]);//remove item in the func
	}
	
	void ProcessAvatar(Radegast.Rendering.RenderAvatar av)
	{
		GameObject avatarGameObject = new GameObject(av.avatar.LocalID.ToString());
		avatarGameObject.transform.position = new UnityEngine.Vector3(av.avatar.Position.X, av.avatar.Position.Y, -av.avatar.Position.Z);
		foreach (Radegast.Rendering.GLMesh mesh in av.glavatar._meshes.Values)
		{
			if (av.glavatar._showSkirt == false && mesh.Name == "skirtMesh") continue;
			
			UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[mesh.RenderData.Vertices.Length / 3];
			UnityEngine.Vector2[] uvs = new UnityEngine.Vector2[mesh.RenderData.Vertices.Length / 3];
			UnityEngine.Vector3[] normals = new UnityEngine.Vector3[mesh.RenderData.Vertices.Length / 3];
			int[] triangles = new int[mesh.RenderData.Indices.Length];
			for (int i = 0; i < mesh.RenderData.Vertices.Length / 3; ++i)
			{
				vertices[i].x = mesh.RenderData.Vertices[3*i];
				vertices[i].y = mesh.RenderData.Vertices[3*i+1];
				vertices[i].z = -mesh.RenderData.Vertices[3*i+2];
				
				uvs[i].x = mesh.RenderData.TexCoords[2*i];
				uvs[i].y = mesh.RenderData.TexCoords[2*i+1];
				
				normals[i].x = mesh.RenderData.Normals[3*i];
				normals[i].y = mesh.RenderData.Normals[3*i+1];
				normals[i].z = -mesh.RenderData.Normals[3*i+2];
			}
			
			for (int i = 0; i < mesh.RenderData.Indices.Length; i += 3)
			{
				//HACK: OpenGL's default front face is counter-clock-wise
				triangles[i] = mesh.RenderData.Indices[i+2];
				triangles[i+1] = mesh.RenderData.Indices[i+1];
				triangles[i+2] = mesh.RenderData.Indices[i];
			}
			
			GameObject part = new GameObject(mesh.Name);
			MeshRenderer mr = part.AddComponent("MeshRenderer") as MeshRenderer;	
			if (Application.platform == RuntimePlatform.Android)
				mr.material = avMaterials[(AvatarTextureIndex)mesh.teFaceID];
			
			part.AddComponent("MeshFilter");
			part.transform.parent = avatarGameObject.transform;
			if (mesh.Name == "eyelashMesh")
				part.transform.localPosition = new UnityEngine.Vector3(-mesh.Position.X, 0, mesh.Position.Z);
			else
				part.transform.localPosition = new UnityEngine.Vector3(0, 0, 0);
			UnityEngine.Mesh meshUnity = (part.GetComponent<MeshFilter>() as MeshFilter).mesh;
							
			meshUnity.vertices = vertices;
			meshUnity.uv = uvs;
			meshUnity.triangles = triangles;
			meshUnity.normals = normals;
		}
		avatars.Add(av.avatar.LocalID, avatarGameObject);
		renderAvatars.Add(av.avatar.LocalID, av);
		avatarGameObject.transform.parent = avatarObjects.transform;
	}

	void MoveAvatars()
	{	
		//althouth TerseObjectUpdate event can give us the new coord of the avatar, but the coord is updated by openmetaverse in the background already.
		foreach (var item in avatars)
		{	
			GameObject av = item.Value;
			Radegast.Rendering.RenderAvatar ra = renderAvatars[item.Key];
			UnityEngine.Vector3 pos;
			pos.x = ra.BasePrim.Position.X;
			pos.y = ra.BasePrim.Position.Y;
			pos.z = -(ra.BasePrim.Position.Z - 0.7f); //hack: fix foot to ground
			av.transform.position = pos;
			
			UnityEngine.Quaternion rot;
			rot.x = ra.BasePrim.Rotation.X;
			rot.y = ra.BasePrim.Rotation.Y;
			rot.z = ra.BasePrim.Rotation.Z;
			rot.w = ra.BasePrim.Rotation.W;
			av.transform.rotation = rot;
		}
	}
	
	public void Clear()
	{
		foreach (var item in avatars)
			UnityEngine.Object.Destroy(item.Value);
		avatars.Clear();
		renderAvatars.Clear();
		newAvatars.Clear();
		avHasTex.Clear();
	}
	
	void DownloadAVTextures(OpenMetaverse.Avatar a)
	{
		foreach (Primitive.TextureEntryFace TEF in a.Textures.FaceTextures)
		{			
			if (TEF == null) continue;
			m_textures.DownloadTexture(TEF.TextureID);
		}
	}
	
	void Avatars_AvatarAppearance(object sender, AvatarAppearanceEventArgs e)
	{            
		if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return;
		if (e.Simulator.Handle != Client.Network.CurrentSim.Handle) return;

        OpenMetaverse.Avatar a = e.Simulator.ObjectsAvatars.Find(av => av.ID == e.AvatarID);
        if (a != null)
        {
			DownloadAVTextures(a);
			if (!avHasTex.Contains(a.LocalID))
        		avHasTex.Add(a.LocalID);
        }
		Debug.Log("AvatarAppearance for " + a.Name + a.LocalID.ToString());
	}
	
	void UpdateAvTexture(uint avLocalID)
	{		
		GameObject avatarGameObject;
		if (!avatars.TryGetValue(avLocalID, out avatarGameObject))
			return;	//get AvatarAppearence before AvatarUpdate
		bool del = true;
		Radegast.Rendering.RenderAvatar ra = renderAvatars[avLocalID];
		foreach (Radegast.Rendering.GLMesh mesh in ra.glavatar._meshes.Values)
		{
			if (mesh.Name == "skirtMesh") continue;
			UUID texID = ra.avatar.Textures.GetFace((uint)mesh.teFaceID).TextureID;
			
			UnityEngine.Material mat = m_textures.GetMaterial(texID);
			if (mat != null)
			{	
				Transform child = avatarGameObject.transform.FindChild(mesh.Name);
				child.GetComponent<MeshRenderer>().material = mat;
			}
			else
				del = false;
		}
		if (del)
			avHasTex.Remove(avLocalID);
	}
	
	void Appearance_AppearanceSet(object sender, AppearanceSetEventArgs e)
	{
		if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return;
		if (e.Success)
		{
			OpenMetaverse.Avatar me;
			if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out me))
			{
				DownloadAVTextures(me);
				if (!avHasTex.Contains(Client.Self.LocalID))
					avHasTex.Add(Client.Self.LocalID);
			}
			Debug.Log("AppearanceSet for " + Client.Self.Name + Client.Self.LocalID.ToString());
		}
	}
}

public static class Utility
{
	public static MeshmerizerR R = new MeshmerizerR();
	
	public static Texture2D Bitmap2Texture2D(System.Drawing.Bitmap bitmap)
	{
		byte[] byteArray = null;
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            byteArray = stream.ToArray();
        }
		Texture2D tex = new Texture2D(bitmap.Width, bitmap.Height);
		tex.LoadImage(byteArray);
		return tex;
	}
	
	public static void MakeMesh(UnityEngine.Mesh mesh, Face face, int vertices_begin, int vertices_end, int indices_begin, int indices_end, int indices_offset)
	{		
		//we have to clear the mesh first, otherwise there will be exceptions.
		mesh.Clear();
		
		int vertices_count = vertices_end - vertices_begin + 1;
		UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[vertices_count];
		UnityEngine.Vector3[] normals = new UnityEngine.Vector3[vertices_count];
		UnityEngine.Vector2[] uv = new UnityEngine.Vector2[vertices_count];
		for (int k = vertices_begin, i = 0; k <= vertices_end; ++k, ++i)
		{
			vertices[i].x = face.Vertices[k].Position.X;
			vertices[i].y = face.Vertices[k].Position.Y;
			//HACK: unity3d uses left-hand coordinate, so we have to mirror z corrd
			vertices[i].z = -face.Vertices[k].Position.Z;
			
			normals[i].x = face.Vertices[k].Normal.X;
			normals[i].y = face.Vertices[k].Normal.Y;
			//HACK: unity3d uses left-hand coordinate, so we have to mirror z corrd
			normals[i].z = -face.Vertices[k].Normal.Z;
			
			uv[i].x = face.Vertices[k].TexCoord.X;
			//HACK: unity3d uses left-bottom corner as the origin of the texture
			uv[i].y = 1 - face.Vertices[k].TexCoord.Y;
		}
		//indices for this face
		int[] triangles = new int[indices_end - indices_begin + 1];
		for (int k = indices_begin, i = 0; k <= indices_end; k += 3, i += 3)
		{
			//HACK: OpenGL's default front face is counter-clock-wise
			triangles[i] = face.Indices[k + 2] - indices_offset;
			triangles[i + 1] = face.Indices[k + 1] - indices_offset;
			triangles[i + 2] = face.Indices[k + 0] - indices_offset;
		}
		
		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uv;
		mesh.triangles = triangles;
	}
	
	//called in RenderAvatar.cs
	public static string GetCharacterDir()
	{
		//return System.IO.Path.Combine(Application.streamingAssetsPath, "character/");//it is painfull to modify every file op !
		return System.IO.Path.Combine(Application.persistentDataPath, "character/");
	}
	
	public static string GetOpenMetaverseCacheDir()
	{
		return Application.temporaryCachePath;
	}
	
	public static string GetResourceDir()
	{
		return System.IO.Path.Combine(Application.persistentDataPath, "openmetaverse_data/");
	}
	
	public static void InstallCharacterAndOmvResource()
	{
		if (!System.IO.Directory.Exists(GetCharacterDir()) || !System.IO.Directory.Exists(GetResourceDir()))
		{
			Debug.Log("intalling character folder and openmetaverse_data folder");
			if (Application.platform == RuntimePlatform.Android)
				copyDirsFromApk();
			else
				copyDir(Application.streamingAssetsPath, Application.persistentDataPath);
		}
	}
	
	static void copyDirsFromApk()
	{
		copyDirFromApk("character/");
		copyDirFromApk("openmetaverse_data/");
	}
	
	static void copyDirFromApk(string dir)	//dir with / at end
	{
		string[] files = readManifest(System.IO.Path.Combine(Application.streamingAssetsPath, dir + "manifest.txt"));
		foreach (string f in files)
		{
			copyFileFromApk(System.IO.Path.Combine(Application.streamingAssetsPath, dir + f), 
				System.IO.Path.Combine(Application.persistentDataPath, dir + f));
		}
	}
	
	static string[] readManifest(string path)
	{
		WWW www = new WWW(path);
		while(!www.isDone)
			;
		return www.text.Split(new char[]{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);	//Take care of \r! Although I output \n in python script, but on windows a \r is inserted secretly! This will make avatar_lad.xml become avatar_lad.xml\r so you don't get avatar_lad.xml!! This bug took me hours to debug!
	}
	
	static void copyFileFromApk(string SourcePath, string DestinationPath)
	{
		try{
			WWW www = new WWW(SourcePath);
			while(!www.isDone)
				;
	
			if (!Directory.Exists(System.IO.Path.GetDirectoryName(DestinationPath)))
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DestinationPath));
			File.WriteAllBytes(DestinationPath, www.bytes);
		}
		catch (Exception ex)
		{
			Debug.Log(ex.Message);
			Debug.Log("Error occured when copying " + SourcePath + " to " + DestinationPath);
		}
	}
	
	static void copyDir(string SourcePath, string DestinationPath)
	{
		//Now Create all of the directories
		foreach (string dirPath in Directory.GetDirectories(SourcePath, "*", SearchOption.AllDirectories))
    		Directory.CreateDirectory(dirPath.Replace(SourcePath, DestinationPath));

		//Copy all the files
		foreach (string newPath in Directory.GetFiles(SourcePath, "*", SearchOption.AllDirectories))
    		File.Copy(newPath, newPath.Replace(SourcePath, DestinationPath));
	}	
}