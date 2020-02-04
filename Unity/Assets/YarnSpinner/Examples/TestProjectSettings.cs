using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TestProjectSettings : MonoBehaviour
{
    public Text text;

    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        string thingy = "";
        foreach (var languages in ProjectSettings.TextProjectLanguages) {
            thingy += languages + " - ";
        }
        text.text = thingy;
    }
}
