using UnityEngine;

public class EtiketAnimasyonu : MonoBehaviour
{
    [SerializeField] float gecikme = 0f;          // saniye, etiketler sirayla acilsin diye
    [SerializeField] float acilmaSuresi = 0.45f;
    [SerializeField] float yuzmeGenligi = 0.004f; // metre, cerceve icin 0 yapin
    [SerializeField] float yuzmeHizi = 2f;
    [SerializeField] bool kameraYuzu = true;      // cerceve icin kapatin

    Vector3 hedefOlcek;
    Vector3 baslangicKonum;
    float baslangicZamani = -1f;

    void Awake()
    {
        hedefOlcek = transform.localScale;
        baslangicKonum = transform.localPosition;
        transform.localScale = Vector3.zero;
    }

    public void Goster()
    {
        baslangicZamani = Time.time + gecikme;
    }

    public void Gizle()
    {
        baslangicZamani = -1f;
        transform.localScale = Vector3.zero;
    }

    void LateUpdate()
    {
        if (baslangicZamani < 0f) return;

        float t = Mathf.Clamp01((Time.time - baslangicZamani) / acilmaSuresi);
        transform.localScale = hedefOlcek * EaseOutBack(t);
        transform.localPosition = baslangicKonum +
            Vector3.up * (Mathf.Sin(Time.time * yuzmeHizi) * yuzmeGenligi);

        if (kameraYuzu && Camera.main != null)
        {
            Vector3 yon = transform.position - Camera.main.transform.position;
            if (yon.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(yon);
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }
}