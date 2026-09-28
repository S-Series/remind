{
  "format": "REmindChart",
  "formatVersion": 1,
  "musicId": "effect_gameplay_sample",
  "difficultyId": "demo",
  "gimmickId": "sample",
  "baseBpm": 225.0,
  "musicStartCorrectionMs": 0.0,
  "revision": "stage6_demo_004",
  "notes": [
    "001|1200|LF------|--------|00000000|-1|-|F|-",
    "001|2400|--------|--------|00000000|-1|-|T|-",
    "001|3000|--------|--------|00000000|-1|-|T|-",
    "001|3600|--LF----|--------|00000000|-1|-|T|-",
    "002|0000|--------|--------|00000000|-1|-|T|-"
  ],
  "eventDictionary": [
    {
      "position": 7800,
      "effectId": "fx_stage6_camera",
      "effectTypeId": "camera.offset",
      "commandId": "",
      "order": 0,
      "parameters": {
        "durationMs": 2000,
        "offsetX": 2.5,
        "offsetY": 0.75,
        "rollDegrees": 12,
        "attackMs": 500,
        "releaseMs": 500
      }
    },
    {
      "position": 7200,
      "effectId": "fx_stage6_begin",
      "effectTypeId": "music.call",
      "commandId": "begin-section",
      "order": 0,
      "parameters": {
        "minimumHealth": 50,
        "damageMultiplier": 0.5
      }
    },
    {
      "position": 8400,
      "effectId": "fx_stage6_count",
      "effectTypeId": "music.call",
      "commandId": "count-success",
      "order": 0
    },
    {
      "position": 9600,
      "effectId": "fx_stage6_end",
      "effectTypeId": "music.call",
      "commandId": "end-section",
      "order": 0
    }
  ]
}
