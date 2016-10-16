﻿using UnityEngine;
using System.Collections;
using Proto4z;
using System;


public class GameScene : MonoBehaviour
{
    private static System.Collections.Generic.Dictionary<ulong, EntityModel> _entitys = new System.Collections.Generic.Dictionary<ulong, EntityModel>();
    private static System.Collections.Generic.Dictionary<ulong, EntityModel> _players = new System.Collections.Generic.Dictionary<ulong, EntityModel>();

	Transform _scene = null;
	float _sceneEndTime = 0f;




	void Awake()
	{
		Facade.GetSingleton<Dispatcher>().AddListener("OnChangeAvatarModel", (System.Action)OnChangeAvatarModel);
		Facade.GetSingleton<Dispatcher>().AddListener("ChangeModeIDResp", (System.Action<ChangeModeIDResp>)OnChangeModeIDResp);

		Facade.GetSingleton<Dispatcher>().AddListener("SceneSectionNotice", (System.Action<SceneSectionNotice>)OnSceneSectionNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("SceneRefreshNotice", (System.Action<SceneRefreshNotice>)OnSceneRefreshNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("AddEntityNotice", (System.Action<AddEntityNotice>)OnAddEntityNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("RemoveEntityNotice", (System.Action<RemoveEntityNotice>)OnRemoveEntityNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("MoveNotice", (System.Action<MoveNotice>)OnMoveNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("AddBuffNotice", (System.Action<AddBuffNotice>)OnAddBuffNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("RemoveBuffNotice", (System.Action<RemoveBuffNotice>)OnRemoveBuffNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("UseSkillNotice", (System.Action<UseSkillNotice>)OnUseSkillNotice);
		Facade.GetSingleton<Dispatcher>().AddListener("MoveResp", (System.Action<MoveResp>)OnMoveResp);
		Facade.GetSingleton<Dispatcher>().AddListener("UseSkillResp", (System.Action<UseSkillResp>)OnUseSkillResp);

		Facade.GetSingleton<Dispatcher>().AddListener("OnAvatarAttack", (System.Action)OnAvatarAttack);

	}

    void Start ()
    {

	}

    void FixedUpdate()
    {

    }

	public Transform GetScene()
	{
		return _scene;
	}
	public float GetSceneCountdown()
	{
		if (_scene == null)
			return 0;
		return _sceneEndTime - Time.realtimeSinceStartup;
	}

	public void DestroyCurrentScene()
	{
		if (_scene != null) 
		{
			CleanEntity();
			GameObject.Destroy(_scene.gameObject);
			_scene = null;
			Facade._mainUI._skillPanel.gameObject.SetActive(false);
			Facade._mainUI._touchPanel.gameObject.SetActive(false);
			Facade._mainUI.SetActiveBG(true);
            Facade._mainUI._selectScenePanel.GetComponent<AudioSource>().Play(0);
		}

	}

    public void RefreshEntityMove(Proto4z.EntityMoveArray moves)
    {
        foreach (var mv in moves)
        {
            var entity = GetEntity(mv.eid);
            if (entity != null)
            {
                entity._info.entityMove = mv;
            }
        }
    }
    public void RefreshEntityInfo(Proto4z.EntityInfoArray infos)
    {
        foreach (var info in infos)
        {
            var entity = GetEntity(info.eid);
            if (entity != null)
            {
                entity._info.entityInfo = info;
            }
        }
    }
    public EntityModel GetEntity(ulong entityID)
    {
        EntityModel ret = null;
        _entitys.TryGetValue(entityID, out ret);
        return ret;
    }
    public EntityModel GetPlayer(ulong avatarID)
    {
        EntityModel ret = null;
        _players.TryGetValue(avatarID, out ret);
        return ret;
    }
	public void CleanEntity()
	{
        foreach (var e in _entitys)
        {
			if (e.Value._info.entityInfo.eid == Facade._entityID) 
			{
				Facade._entityID = 0;
			}
            GameObject.Destroy(e.Value.gameObject);
        }
        _entitys.Clear();
        _players.Clear();
	}
    public void DestroyEntity(ulong entityID)
    {
        var entity = GetEntity(entityID);
        if (entity == null)
        {
            return;
        }
        if (entity._info.entityInfo.eid == Facade._entityID)
        {
            Facade._entityID = 0;
        }
        if (entity._info.baseInfo.avatarID != 0)
        {
            _players.Remove(entity._info.baseInfo.avatarID);
        }
        _entitys.Remove(entityID);
        GameObject.Destroy(entity.gameObject);
    }
    public void DestroyPlayer(ulong avatarID)
    {
        var entity = GetPlayer(avatarID);
        if (entity != null)
        {
            DestroyEntity(entity._info.entityInfo.eid);
        }
    }

    public void CreateEntityByAvatarID(ulong avatarID)
    {
        var entity = GetPlayer(avatarID);
        if (entity != null)
        {
            Debug.LogError("CreateAvatarByAvatarID not found full data");
            return;
        }
        CreateEntity(entity._info);
    }
    public void CreateEntity(Proto4z.EntityFullData data)
    {
        EntityModel oldEnity = GetEntity(data.entityInfo.eid);
        
        Vector3 spawnpoint = new Vector3((float)data.entityMove.pos.x, -13.198f, (float)data.entityMove.pos.y);
        Quaternion quat = new Quaternion();
        if (oldEnity != null && oldEnity != null)
        {
            spawnpoint = oldEnity.gameObject.transform.position;
            quat = oldEnity.gameObject.transform.rotation;
        }

        string name = Facade.GetSingleton<ModelDict>().GetModelName(data.baseInfo.modeID);
        if (name == null)
        {
            name = "jing_ling_nv_001_ty";
        }

        var res = Resources.Load<GameObject>("Character/Model/" + name);
        if (res == null)
        {
            Debug.LogError("can't load resouce model [" + name + "].");
            return;
        }
        var obj = Instantiate(res);
        if (obj == null)
        {
            Debug.LogError("can't Instantiate model[" + name + "].");
            return;
        }

        obj.AddComponent<Rigidbody>();
        if (obj.GetComponent<Animation>() == null)
        {
            obj.AddComponent<Animation>();
        }
        obj.AddComponent<EntityModel>();
        obj.transform.position = spawnpoint;
        if (data.entityInfo.etype == (ushort)Proto4z.EntityType.ENTITY_AVATAR)
        {
            obj.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        }
        else
        {
            obj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        }
        obj.transform.rotation = quat;
        Rigidbody rd = obj.GetComponent<Rigidbody>();
        rd.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        DestroyEntity(data.entityInfo.eid);


		var newEntity = obj.GetComponent<EntityModel>();
        newEntity._info = data;

        _entitys[data.entityInfo.eid] = newEntity;
        if (data.baseInfo.avatarID != 0)
        {
            _players[data.baseInfo.avatarID] = newEntity;
        }
        if (newEntity._info.baseInfo.avatarID == Facade._avatarInfo.avatarID 
            && newEntity._info.entityInfo.etype == (ushort)Proto4z.EntityType.ENTITY_AVATAR)
        {
            Facade._entityID = newEntity._info.entityInfo.eid;
        }

        Debug.Log("create avatar");
    }


	void OnChangeModeIDResp(ChangeModeIDResp resp)
	{
		Debug.logger.Log("ServerProxy::OnChangeModeIDResp ret=" + resp.retCode + ", newModelID= " + resp.modeID );
	}
	void OnChangeAvatarModel()
	{
		Facade.GetSingleton<ServerProxy>().SendToGame(new ChangeModeIDReq(Facade._avatarInfo.modeID%45+1));
	}

	void OnSceneSectionNotice(SceneSectionNotice notice)
	{
		Debug.Log(notice);
		if (_scene != null)
		{
			GameObject.Destroy(_scene.gameObject);
		}
		_sceneEndTime = Time.realtimeSinceStartup + (float)notice.section.sceneEndTime - (float)notice.section.serverTime;
		var scene = Resources.Load<GameObject>("Scene/Home");
		if (scene != null)
		{
			Debug.Log("create scene");
			_scene = Instantiate(scene).transform;
			_scene.gameObject.SetActive(true);
			Facade._mainUI.SetActiveBG(false);
			Facade._mainUI._touchPanel.gameObject.SetActive(true);
			Facade._mainUI._skillPanel.gameObject.SetActive(true);
		}
		else
		{
			Debug.LogError("can't Instantiate [Prefabs/Guis/SelectScene/SelectScene].");
		}
	}
	void OnSceneRefreshNotice(SceneRefreshNotice notice)
	{
		//        Debug.Log(notice);
		Facade._gameScene.RefreshEntityInfo(notice.entityInfos);
		Facade._gameScene.RefreshEntityMove(notice.entityMoves);
	}
	void OnAddEntityNotice(AddEntityNotice notice)
	{
		Debug.Log(notice);
		foreach (var entity in notice.entitys)
		{
			Facade._gameScene.CreateEntity(entity);
		}
	}
	void OnRemoveEntityNotice(RemoveEntityNotice notice)
	{
		Debug.Log(notice);
	}
	void OnMoveNotice(MoveNotice notice)
	{
		if (Facade._entityID != 0 && Facade._entityID == notice.moveInfo.eid)
		{
			if (notice.moveInfo.action != (ushort)Proto4z.MoveAction.MOVE_ACTION_IDLE)
			{
				/*
                var entity = Facade._gameScene.GetEntity(Facade._entityID);
                EntityFullData data = entity._info;
                var binData = data.__encode().ToArray();
                data = new EntityFullData();
                int len = 0;
                data.__decode(binData, ref len);

                var oldPos = data.entityMove.pos;
                data.baseInfo.avatarID = (ulong)Math.Pow(7,50);
                data.baseInfo.avatarName = "快来这里呀";
                data.entityInfo.eid = (ulong)Math.Pow(7, 50);
                data.entityInfo.etype = (ushort)Proto4z.EntityType.ENTITY_FLIGHT;
                data.entityMove.pos = notice.moveInfo.waypoints[0];
                data.entityMove.waypoints.Clear();
                data.entityMove.action = 0;
                Facade._gameScene.CreateEntity(data);
                var newEntity = Facade._gameScene.GetEntity(data.entityInfo.eid).gameObject;
                newEntity.transform.rotation = entity.transform.rotation;
                newEntity.transform.position = new Vector3((float)data.entityMove.pos.x, newEntity.transform.position.y, (float)data.entityMove.pos.y);
  				*/

			}
			else
			{
				DestroyEntity((ulong)Math.Pow(7, 50));
			}

		}

		if (notice.moveInfo.action == (ushort)Proto4z.MoveAction.MOVE_ACTION_IDLE)
		{
			UnityEngine.Debug.Log("[" + DateTime.Now + "]eid=" + notice.moveInfo.eid
				+ ", action=" + notice.moveInfo.action + ", posx=" + notice.moveInfo.pos.x
				+ ", posy=" + notice.moveInfo.pos.y);

		}

	}
	void OnAddBuffNotice(AddBuffNotice notice)
	{
		Debug.Log(notice);
	}
	void OnRemoveBuffNotice(RemoveBuffNotice notice)
	{
		Debug.Log(notice);
	}
	void OnUseSkillNotice(UseSkillNotice notice)
	{
		var entity = GetEntity (notice.eid);
		if (entity == null)
		{
			return;
		}
		entity.CrossAttack ();
	}
	void OnMoveResp(MoveResp resp)
	{
		Debug.Log(resp);
	}


	void OnUseSkillResp(UseSkillResp resp)
	{
		Debug.Log(resp);
	}	

	void OnAvatarAttack()
	{
		Facade.GetSingleton<ServerProxy> ().SendToScene (new UseSkillReq (Facade._entityID));
	}

}
