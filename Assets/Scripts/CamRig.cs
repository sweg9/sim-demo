using UnityEngine;

public enum CamMode { Chase, Overview, Close, Pov }

/// <summary>Main camera: chase / dollhouse overview / over-the-shoulder close-up / robot point of view.</summary>
public class CamRig : MonoBehaviour
{
    public Camera cam;
    public RobotRig robot;
    public House house;
    public CamMode mode = CamMode.Overview;

    float yaw = 215f, pitch = 50f, dist = 19f;
    float chaseYaw, chasePitch = 33f, chaseDist = 5.4f;
    float userT;
    Vector3 pivot = new Vector3(6.5f, 0.6f, 4.5f);
    Vector3 pos;
    Quaternion rot = Quaternion.identity;
    float fov = 45f;
    bool initDone;

    // temporary overrides
    Vector3 glanceTarget, glanceFrom;
    float glanceUntil;
    Transform closeTarget;
    Vector3 closePoint;
    public bool autoCinema = true;

    float[] wallH = { 1, 1, 1, 1 };
    static readonly string[] wallKeys = { "S", "N", "W", "E" };

    public void SetMode(CamMode m) { mode = m; userT = Time.time; }

    public void Cycle()
    {
        mode = (CamMode)(((int)mode + 1) % 4);
        if (mode == CamMode.Close) mode = CamMode.Pov;
        userT = Time.time;
    }

    public void CloseUp(Vector3 point) { closePoint = point; mode = CamMode.Close; }

    public void Glance(Vector3 target, Vector3 from, float seconds)
    {
        glanceTarget = target; glanceFrom = from; glanceUntil = Time.time + seconds;
    }

    void Start()
    {
        if (cam == null) cam = GetComponent<Camera>();
    }

    void Update()
    {
        // user input
        if (Input.GetMouseButton(1) || (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftAlt)))
        {
            float dx = Input.GetAxis("Mouse X") * 3.2f, dy = Input.GetAxis("Mouse Y") * 2.6f;
            if (mode == CamMode.Overview) { yaw += dx; pitch = Mathf.Clamp(pitch - dy, 12f, 88f); }
            else { chaseYaw += dx; chasePitch = Mathf.Clamp(chasePitch - dy, 8f, 80f); userT = Time.time; }
            if (mode == CamMode.Close || mode == CamMode.Pov) mode = CamMode.Chase;
        }
        float sc = Input.mouseScrollDelta.y;
        if (Mathf.Abs(sc) > 0.01f)
        {
            if (mode == CamMode.Overview) dist = Mathf.Clamp(dist * (1f - sc * 0.06f), 5f, 34f);
            else chaseDist = Mathf.Clamp(chaseDist * (1f - sc * 0.06f), 1.6f, 14f);
        }
        float orb = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        if (orb != 0f)
        {
            if (mode == CamMode.Overview) yaw += orb * 70f * Time.deltaTime;
            else { chaseYaw += orb * 70f * Time.deltaTime; userT = Time.time; }
        }
    }

    void LateUpdate()
    {
        if (cam == null || robot == null) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        Vector3 dPos; Quaternion dRot; float dFov;
        Vector3 rp = robot.transform.position;

        if (Time.time < glanceUntil)
        {
            Vector3 look = glanceTarget;
            dPos = look + glanceFrom;
            dRot = Quaternion.LookRotation(look - dPos, Vector3.up);
            dFov = 50f;
        }
        else switch (mode)
        {
            case CamMode.Pov:
                dPos = robot.eyeCam.transform.position;
                dRot = robot.eyeCam.transform.rotation;
                dFov = robot.eyeCam.fieldOfView;
                break;
            case CamMode.Close:
            {
                Vector3 f = robot.transform.forward, r = robot.transform.right;
                dPos = rp - f * 0.95f + r * 0.95f + Vector3.up * 1.55f;
                dRot = Quaternion.LookRotation(closePoint - dPos, Vector3.up);
                dFov = 38f;
                break;
            }
            case CamMode.Overview:
            {
                Quaternion q = Quaternion.Euler(pitch, yaw, 0);
                dPos = pivot - q * Vector3.forward * dist;
                dRot = q;
                dFov = 38f;
                break;
            }
            default:
            {
                // auto-follow the heading unless the user has just orbited
                if (Time.time - userT > 2.5f)
                    chaseYaw = Mathf.LerpAngle(chaseYaw, robot.transform.eulerAngles.y, 1f - Mathf.Exp(-0.9f * dt));
                Quaternion q = Quaternion.Euler(chasePitch, chaseYaw, 0);
                Vector3 target = rp + Vector3.up * 0.9f;
                dPos = target - q * Vector3.forward * chaseDist;
                dRot = q;
                dFov = 46f;
                break;
            }
        }

        if (!initDone) { pos = dPos; rot = dRot; fov = dFov; initDone = true; }
        float k = 1f - Mathf.Exp(-(mode == CamMode.Pov && Time.time >= glanceUntil ? 30f : 3.2f) * dt);
        pos = Vector3.Lerp(pos, dPos, k);
        rot = Quaternion.Slerp(rot, dRot, k);
        fov = Mathf.Lerp(fov, dFov, k);
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;

        // hide the ceiling when looking from above, lower the walls that face the camera when outside
        bool above = pos.y > 2.4f || !house.footprint.Contains(new Vector2(pos.x, pos.z));
        if (above) cam.cullingMask &= ~(1 << Util.MinimapLayer); else cam.cullingMask |= (1 << Util.MinimapLayer);
        Vector3 p = pos;
        bool[] hide = { p.z < -0.3f, p.z > 9.3f, p.x < -0.3f, p.x > 13.3f };
        for (int i = 0; i < 4; i++)
        {
            Transform g;
            if (!house.wallGroups.TryGetValue(wallKeys[i], out g)) continue;
            wallH[i] = Mathf.MoveTowards(wallH[i], hide[i] ? 0.05f : 1f, dt * 2.2f);
            g.localScale = new Vector3(1, wallH[i], 1);
        }
    }
}
