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
		
	string m_userName = "qiang cao";
	string m_password = "1234";
	string m_server = "http://192.168.1.134:9000";
	
	GridClient Client = new GridClient();
	
	public LogInOutProcess m_loginout;
	TerrainProcess m_terrain;
	TextureProcess m_textures;
	ObjectProcess m_objects;
	CameraProcess m_camera;
	SelfMoveProcess m_selfmove;
	AvatarsProcess m_avatars;
	
	// Use this for initialization
	void Start () {
		m_loginout = new LogInOutProcess(Client);
		m_terrain = new TerrainProcess(Client);
		m_textures = new TextureProcess(Client);
		m_objects = new ObjectProcess(Client, m_textures);
		m_camera = new CameraProcess(Client);
		m_selfmove = new SelfMoveProcess(Client);
		m_avatars = new AvatarsProcess(Client, m_textures);
	}
	
	// Update is called once per frame
	void Update () {
		if (Client.Network.Connected)
		{						
			m_terrain.Update();
			m_objects.Update();
			m_camera.Update();
			m_selfmove.Update();
			m_avatars.Update();
		}
		if (m_loginout == null)
			Debug.Log("m_loginout == null");
	}
	
	void OnGUI()
	{
		if (Client.Network.Connected == false)
		{
			GUILayout.Label("用户名", GUILayout.Height(50));
			m_userName = GUILayout.TextField(m_userName, GUILayout.Height(50));
			GUILayout.Label("密码", GUILayout.Height(50));
			m_password = GUILayout.TextField(m_password, GUILayout.Height(50));
			GUILayout.Label("登录地址", GUILayout.Height(50));
			m_server = GUILayout.TextField(m_server, GUILayout.Height(50));
			if (GUILayout.Button("登录", GUILayout.Height(100)))
				m_loginout.StartLogin(m_userName, m_password, m_server);
		}
		else
		{
			if (GUILayout.Button("登出", GUILayout.Height(100)))
			{
				m_loginout.LogOut();
				ClearScene();
			}
		}
	}
	
	void ClearScene()
	{		
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
	const float MinimumTimeBetweenTerrainUpdated = 2f;	//in second
	float terrainTimeSinceUpdate = MinimumTimeBetweenTerrainUpdated + 1f;
		
	GridClient Client;
		
	public TerrainProcess(GridClient client)
	{		
		Client = client;

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
		Simulator sim = Client.Network.CurrentSim;
        terrainImage = Radegast.Rendering.TerrainSplat.Splat(Client, heightTable,
            new UUID[] { sim.TerrainDetail0, sim.TerrainDetail1, sim.TerrainDetail2, sim.TerrainDetail3 },
            new float[] { sim.TerrainStartHeight00, sim.TerrainStartHeight01, sim.TerrainStartHeight10, sim.TerrainStartHeight11 },
            new float[] { sim.TerrainHeightRange00, sim.TerrainHeightRange01, sim.TerrainHeightRange10, sim.TerrainHeightRange11 });
		
		Texture2D tex = Utility.Bitmap2Texture2D(terrainImage);
		terrainPart1.GetComponent<MeshRenderer>().material.mainTexture = tex;
		terrainPart2.GetComponent<MeshRenderer>().material.mainTexture = tex;
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
	Dictionary<UUID, Texture2D> textures = new Dictionary<UUID, Texture2D>();
	Dictionary<UUID, Bitmap> bitmaps = new Dictionary<UUID, Bitmap>();
	
	public TextureProcess(GridClient client)
	{
		Client = client;
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
		if (!textures.ContainsKey(textureID))
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
}

public class ObjectProcess
{
	GridClient Client;
	TextureProcess m_textures;
	Dictionary<uint, FacetedMesh> newPrims = new Dictionary<uint, FacetedMesh>();
	Dictionary<uint, GameObject> objects = new Dictionary<uint, GameObject>();
	GameObject primObjects = new GameObject("prims");
	
	public ObjectProcess(GridClient client, TextureProcess textureProcess)
	{
		Client = client;
		m_textures = textureProcess;
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
			foreach (Face face in mesh.Faces)
			{
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
			
		GameObject obj = new GameObject(mesh.Prim.LocalID.ToString());
		
		GameObject parent = null;
		if (mesh.Prim.ParentID != 0)
		{
			if (!objects.ContainsKey(mesh.Prim.ParentID))
			{
				if (newPrims.ContainsKey(mesh.Prim.ParentID) == false)
					Debug.Break();
				ProcessPrim(newPrims[mesh.Prim.ParentID]);	//it seems that the parent is received before children
			}
			parent = objects[mesh.Prim.ParentID];			
		}
		else
			parent = primObjects;
		
		// Create vertices, uv, triangles for EACH FACE that stores the 3D data in Unity3D friendly format
		for (int j = 0; j < mesh.Faces.Count; j++)
	    {
			Face face = mesh.Faces[j];
			GameObject faceObj = new GameObject("face" + j.ToString());
			faceObj.transform.parent = obj.transform;
			UnityEngine.Material mat = (faceObj.AddComponent("MeshRenderer") as MeshRenderer).material;
			
			Texture2D tex = m_textures.GetTexture2D(face.TextureFace.TextureID);
			if (tex != null)
				mat.mainTexture = tex;
			else
				Debug.Log("mat is null");
				//mat = m;
			
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
	
	public CameraProcess(GridClient client)
	{
		Client = client;
	}
	
	public void Update()
	{
		Camera.main.clearFlags = CameraClearFlags.Skybox;
		
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
	}
}

public class SelfMoveProcess
{
	GridClient Client;
	bool isHoldingHome = false;
	
	public SelfMoveProcess(GridClient client)
	{
		Client = client;
	}
	
	public void Update()
	{
		float time = Time.deltaTime;
		
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
			part.AddComponent("MeshRenderer");	
			part.AddComponent("MeshFilter");
			part.transform.parent = avatarGameObject.transform;
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
		if (e.Simulator.Handle != Client.Network.CurrentSim.Handle) return;

        OpenMetaverse.Avatar a = e.Simulator.ObjectsAvatars.Find(av => av.ID == e.AvatarID);
        if (a != null)
        {
			DownloadAVTextures(a);
        	avHasTex.Add(a.LocalID);
        }
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

			Texture2D tex = m_textures.GetTexture2D(texID);
			if (tex != null)
			{	
				Transform child = avatarGameObject.transform.FindChild(mesh.Name);
				UnityEngine.Material mat = child.GetComponent<MeshRenderer>().material;
				mat.mainTexture = tex;
			}
			else
				del = false;
		}
		if (del)
			avHasTex.Remove(avLocalID);
	}
	
	void Appearance_AppearanceSet(object sender, AppearanceSetEventArgs e)
	{
		if (e.Success)
		{
			OpenMetaverse.Avatar me;
			if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out me))
			{
				DownloadAVTextures(me);
				if (!avHasTex.Contains(Client.Self.LocalID))
					avHasTex.Add(Client.Self.LocalID);
			}
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
}