using UnityEngine;

public class NotePosition : MonoBehaviour
{
    private const float MaxRange = 14.85f;
    public void SetPosition(float PosX, float PosY)
    {
        transform.position = new Vector2(PosX, PosY);
    }
    public void SetPositionX(float PosX)
    {
        Vector2 prev = transform.position;
        transform.position = new Vector2(PosX, prev.y);
    }
    public void SetPositionY(float PosY)
    {
        Vector2 prev = transform.position;
        transform.position = new Vector2(prev.x, PosY);
    }

    public void SetPosXByLine(int line)
    {
        double pct = GetPctByLine(line);
        SetPosXByPct(pct);
    }
    public void SetPosXByPct(double pct)
    {
        float value = Mathf.Clamp((float)pct,-MaxRange,MaxRange);
        SetPositionX(value);
    }
    public void SetPosYByGridData(int gridNum, double pct)
    {
        float valueY = 160.0f * (gridNum + (float)(pct / 100));
        SetPositionY(valueY);
    }
    public void SetPosByDataWithLine(int line, int gridNum, double pctY)
    {
        double pctX = GetPctByLine(line);
        SetPosByDataWithPct(pctX, gridNum, pctY);

    }
    public void SetPosByDataWithPct(double pctX, int gridNum, double pctY)
    {
        float valueX = Mathf.Clamp((float)pctX,-MaxRange,MaxRange);
        float valueY = 160.0f * (gridNum + (float)(pctY / 100));
        SetPosition(valueX, valueY);
    }

    private double GetPctByLine(int line)
    {
        return line switch
        {
            0 => 0.0,
            //==========
            1 => 0.125,
            2 => 0.375,
            3 => 0.625,
            4 => 0.875,
            //==========
            -1 => 0.25,
            -2 => 0.75,
            //==========
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(line),
                line,
                "지원하지 않는 라인 번호입니다."
            )
        };
    }
}
