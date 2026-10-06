using System;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 我们自己的专属镜头。
    //
    // 思路（用户提出的方案，已核实可行）：不去借用游戏原有的追尾镜头，而是用游戏自己的追尾镜头类型
    // **构造一个属于我们的实例**，把它注册进游戏的镜头列表，再通过游戏的切换流程切过去。
    // 这样：
    //   1. 游戏原有的镜头（追尾/环绕/座舱/玩家作品里的相机零件）完全不被触碰；
    //   2. 我们的镜头是游戏镜头列表里的一个正式条目，切过去之后"当前视角是什么"就不再重要；
    //   3. 位置、朝向、阻尼、贴地避让仍由游戏自己的追尾镜头逻辑负责，HUD（航炮准星、锁定圈）自然一致。
    //
    // 关键接口（IL 转储）：
    //   ChaseCameraController(CameraManagerScript, bool centerOnRigidBody, float startDistance, PartScript)
    //   CameraManagerScript.AddCamera(CameraController)         → 就是 _cameraControllers.Add
    //   CameraManagerScript.SwitchToCamera(CameraController)    → 走游戏自己的切换流程
    //   CameraManagerScript.UnregisterCustomCameraVantage(CameraController) → 从列表里摘掉
    // 我们构造好后把它的 _transformToTrack 指向自己的瞄准物体，并打开 StayUpright 保持水平。
    internal sealed class CameraAimRig
    {
        private GameObject _object;
        private Transform _transform;
        private Func<Transform> _provider;

        private object _controller;
        private string _trackedMember;
        private object _originalTarget;
        private object _originalStayUpright;
        private bool _stayUprightApplied;
        private bool _registered;
        private string _controllerType;
        private string _cameraName;
        private float _baseRotationDamp;
        private bool _hasBaseRotationDamp;
        private bool _hooked;


        public object Controller
        {
            get { return _controller; }
        }

        public string ControllerType
        {
            get { return _controllerType; }
        }

        public string TrackedMember
        {
            get { return _trackedMember; }
        }

        public bool StayUprightApplied
        {
            get { return _stayUprightApplied; }
        }


        public bool HasBaseDamp
        {
            get { return _hasBaseRotationDamp; }
        }

        public float BaseRotationDamp
        {
            get { return _baseRotationDamp; }
        }

        public string CameraName
        {
            get { return _cameraName; }
        }

        // 每帧把我们的物体摆到瞄准姿态上，供我们自己的镜头跟随。
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            EnsureObject();
            _transform.position = position;
            _transform.rotation = rotation;
        }

        public bool TryCreate(string cameraName, float distance, float followDampScale, out string error)
        {
            return TryCreateCore(cameraName, distance, followDampScale, false, out error);
        }

        public bool TryCreateOrbit(string cameraName, float distance, out string error)
        {
            return TryCreateCore(cameraName, distance, 1f, true, out error);
        }

        private bool TryCreateCore(string cameraName, float distance, float followDampScale, bool orbit, out string error)
        {
            ReleaseHook();

            if (!CameraInputPatch.IsInstalled)
            {
                error = "专属镜头输入补丁不可用";
                return false;
            }

            EnsureObject();

            if (_provider == null)
            {
                _provider = new Func<Transform>(GetAimTransform);
            }

            object manager;
            if (!GameApi.TryGetCameraManager(out manager, out error))
            {
                return false;
            }

            object aircraft;
            if (!GameApi.TryGetLocalAircraft(out aircraft, out error))
            {
                return false;
            }

            object cockpit;
            string cockpitError;
            if (!GameApi.TryGetAircraftCockpitPart(aircraft, out cockpit, out cockpitError))
            {
                // 零件只是构造参数，取不到就以空传入；位置与朝向实际由我们的瞄准物体决定。
                cockpit = null;
            }

            object controller;
            bool created = orbit
                ? GameApi.TryConstructOwnOrbitCamera(manager, _provider, out controller, out error)
                : GameApi.TryConstructOwnChaseCamera(manager, cockpit, distance, out controller, out error);
            if (!created)
            {
                error = "构造专属镜头失败：" + error;
                return false;
            }

            // 改名：游戏切换视角时弹出的提示、以及记进设置里的视角名都会用它。
            string nameError;
            if (!GameApi.TrySetCameraName(controller, cameraName, out nameError))
            {
                error = "给专属镜头改名失败：" + nameError;
                return false;
            }

            object original;
            string memberUsed;
            string controllerType;
            if (!GameApi.TryHookCameraTarget(controller, _provider, out original, out memberUsed, out controllerType, out error))
            {
                error = "给专属镜头设置跟随目标失败：" + error;
                return false;
            }

            _controller = controller;
            _trackedMember = memberUsed;
            _originalTarget = original;
            _controllerType = controllerType;
            _cameraName = cameraName;
            _hooked = true;

            // 保持水平、不随机身滚转：游戏自带的功能，战雷那种手感。
            object stayUpright;
            _stayUprightApplied = GameApi.TryForceStayUpright(controller, out stayUpright, out error);
            _originalStayUpright = _stayUprightApplied ? stayUpright : null;

            if (!GameApi.TryAddCamera(controller, out error))
            {
                error = "注册专属镜头失败：" + error;
                ReleaseHook();
                return false;
            }

            _registered = true;
            CameraInputPatch.Bind(_controller);

            // 镜头跟随瞄准方向的收敛速度：读游戏给自己那个镜头设的阻尼值，按系数缩放。
            float baseDamp;
            string dampError;
            if (!orbit && GameApi.TryGetRotationDamp(controller, out baseDamp, out dampError))
            {
                _baseRotationDamp = baseDamp;
                _hasBaseRotationDamp = true;
                ApplyFollowDamp(followDampScale, out dampError);
            }
            else
            {
                _hasBaseRotationDamp = false;
            }

            error = null;
            return true;
        }

        // 每帧确认阻尼值没被游戏改回去（镜头是我们构造的，游戏一般不碰，但保险起见）。
        public void EnsureFollowDamp(float followDampScale)
        {
            if (!_hooked || !_hasBaseRotationDamp || _controller == null)
            {
                return;
            }

            string error;
            ApplyFollowDamp(followDampScale, out error);
        }

        private void ApplyFollowDamp(float followDampScale, out string error)
        {
            float target = _baseRotationDamp * followDampScale;
            if (target < 0.05f)
            {
                target = 0.05f;
            }

            GameApi.TrySetRotationDamp(_controller, target, out error);
        }

        // 游戏在换作品、重载场景等情况下可能清空镜头列表，这时需要重新注册。
        public bool EnsureRegistered(out bool registered, out string error)
        {
            registered = false;
            error = null;

            if (!_hooked || _controller == null)
            {
                return false;
            }

            bool isRegistered;
            if (!GameApi.IsCameraControllerRegistered(_controller, out isRegistered, out error))
            {
                return false;
            }

            if (isRegistered)
            {
                _registered = true;
                return true;
            }

            if (!GameApi.TryAddCamera(_controller, out error))
            {
                return false;
            }

            _registered = true;
            registered = true;
            return true;
        }

        public bool ZeroLookOffset(out string error)
        {
            if (!_hooked)
            {
                error = "尚未接管";
                return false;
            }

            return GameApi.TryZeroCameraLookOffset(_controller, out error);
        }

        public void ReleaseHook()
        {
            CameraInputPatch.Unbind(_controller);
            string error;

            if (_hooked && _controller != null)
            {
                if (_stayUprightApplied)
                {
                    GameApi.TryRestoreStayUpright(_controller, _originalStayUpright, out error);
                }

                GameApi.TryRestoreCameraTarget(_controller, _trackedMember, _originalTarget, out error);

                if (_registered)
                {
                    GameApi.TryUnregisterCamera(_controller, out error);
                }
            }

            _hooked = false;
            _registered = false;
            _controller = null;
            _controllerType = null;
            _trackedMember = null;
            _originalTarget = null;
            _originalStayUpright = null;
            _stayUprightApplied = false;
            _hasBaseRotationDamp = false;
        }

        public void Destroy()
        {
            ReleaseHook();

            if (_object != null)
            {
                UnityEngine.Object.Destroy(_object);
                _object = null;
                _transform = null;
                _provider = null;
            }
        }

        private Transform GetAimTransform()
        {
            return _transform;
        }

        private void EnsureObject()
        {
            if (_object != null)
            {
                return;
            }

            _object = new GameObject("MouseFlightAimRig");
            _transform = _object.transform;
        }
    }
}
