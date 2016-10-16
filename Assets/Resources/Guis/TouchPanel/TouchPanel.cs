﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
using Proto4z;


public class TouchPanel : MonoBehaviour
{

    UnityEngine.EventSystems.EventSystem _event;
    Camera _mainCamera;

    public Image strick;
    Vector3 _originStrick;
    Vector3 _lastDirt;
    bool _isStrick = false;
    EntityModel _control;
    float _lastSendMove = 0.0f;

    void Start ()
    {
        _event = UnityEngine.EventSystems.EventSystem.current;
    }
    void BeginStrick(Vector3 position)
    {
        _isStrick = true;
        _originStrick = position;
        strick.gameObject.SetActive(true);
        strick.transform.position = position;
    }
    void EndStrick()
    {
        if (_isStrick)
        {
            _isStrick = false;
            strick.gameObject.SetActive(false);
            var req = new MoveReq();
            req.eid = Facade._entityID;
            req.action = (ushort)Proto4z.MoveAction.MOVE_ACTION_IDLE;
            req.clientPos = new Proto4z.EPoint(_control.transform.position.x, _control.transform.position.z);
            req.dstPos.x = _control.transform.position.x;
            req.dstPos.y = _control.transform.position.z;
            Facade.GetSingleton<ServerProxy>().SendToScene(req);
            Debug.Log("client stop move EndStrick");
        }
    }

    void AvatarAttack()
    {
        _control.CrossAttack();
    }

    void CheckStrick(Vector3 position)
    {
        var dist = Vector3.Distance(position, _originStrick);
        if (dist < 0.3f)
        {
            return;
        }
        if (dist > Screen.width * GameOption._TouchRedius)
        {
            dist = Screen.width * GameOption._TouchRedius;
        }
        var dir = Vector3.Normalize(position - _originStrick);
        strick.transform.position = _originStrick + (dir * dist);


        
        dir.z = dir.y;
        dir.y = 0;
        dir *= 20;
        if (_lastDirt == null || Vector3.Distance(dir, _lastDirt) > Math.Sin( 10.0 * Math.PI/360.0)* 20)
        {
            _lastDirt = dir;
        }
        else if (Time.realtimeSinceStartup - _lastSendMove < 1f)
        {
            return;
        }

        Vector3 dst = _control.transform.position + _lastDirt;
        _lastSendMove = Time.realtimeSinceStartup;
        Debug.Log("Send Move");
        var req = new MoveReq();
        req.eid = Facade._entityID;
        req.action = (ushort)Proto4z.MoveAction.MOVE_ACTION_PATH;
        req.clientPos = new Proto4z.EPoint(_control.transform.position.x, _control.transform.position.z);
        req.dstPos.x += dst.x;
        req.dstPos.y += dst.z;
        Facade.GetSingleton<ServerProxy>().SendToScene(req);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Facade._entityID == 0)
        {
            return;
        }
        if (_control != null && _control._info.entityInfo.eid != Facade._entityID)
        {
            _control = null;
        }
        if (_control == null)
        {
            _control = Facade._sceneManager.GetEntity(Facade._entityID);
            foreach(Camera camera in Camera.allCameras)
            {
                if (camera.name == "SceneCamera")
                {
                    _mainCamera = camera;
                    break;
                }
            }
            _isStrick = false;
        }
            
        /*
        if (true)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            var req = new MoveReq();
            req.eid = Facade._entityID;
            req.clientPos = new Proto4z.EPoint(_control.transform.position.x, _control.transform.position.z);
            req.dstPos.x = _control.transform.position.x;
            req.dstPos.y = _control.transform.position.z;

            if (Math.Abs(h) > 0.1 || Math.Abs(v) > 0.1)
            {
                _isHandle = true;
                req.action = (ushort)Proto4z.MoveAction.MOVE_ACTION_PATH;
                req.dstPos.x += h * 10;
                req.dstPos.y += v * 10;
                Facade.GetSingleton<ServerProxy>().SendToScene(req);
            }
            else if (_isHandle && _control._info.entityMove.action != (ushort) Proto4z.MoveAction.MOVE_ACTION_IDLE)
            {
                _isHandle = false;
                req.action = (ushort)Proto4z.MoveAction.MOVE_ACTION_IDLE;
                Facade.GetSingleton<ServerProxy>().SendToScene(req);
            }
        }
        */

        if (Input.GetMouseButtonDown(0))
        {
            if (RectTransformUtility.RectangleContainsScreenPoint((transform as RectTransform), new Vector2(Input.mousePosition.x, Input.mousePosition.y)))
            {
                BeginStrick(Input.mousePosition);
            }
			else if(GameOption._EnbaleClickMove && !_event.IsPointerOverGameObject())
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit3D = new RaycastHit();
                Physics.Raycast(ray, out hit3D, 100);
                if (hit3D.transform != null && hit3D.transform.name == "Terrain")
                //if (hit3D.transform != null )
                {
                    var req = new MoveReq();
                    req.eid = Facade._entityID;
                    req.action = (ushort)Proto4z.MoveAction.MOVE_ACTION_PATH;
                    req.clientPos = new Proto4z.EPoint(_control.transform.position.x, _control.transform.position.z);
                    req.dstPos.x = hit3D.point.x;
                    req.dstPos.y = hit3D.point.z;
                    Facade.GetSingleton<ServerProxy>().SendToScene(req);
                }
            }
            
        }
        if (Input.GetMouseButtonUp(0))
        {
            EndStrick();
        }
        if (_isStrick)
        {
            CheckStrick(Input.mousePosition);
        }
    }
}
