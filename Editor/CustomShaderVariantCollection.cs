using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "CustomShaderVariantCollection")]
public class CustomShaderVariantCollection : ScriptableObject, IPreprocessShaders
{
    public bool enabled = true;
    
    public TextAsset text;
    public List<Shader> shaders;
    public List<ShaderEntry> shaderEntries;
    
    [Serializable]
    public class ShaderEntry
    {
        public List<string> passNames = new List<string>();
        public List<KeywordSet> keywordSets = new List<KeywordSet>();
    }

    [Serializable]
    public class KeywordSet
    {
        public List<KeywordEntry> keywordEntries = new List<KeywordEntry>();
    }

    [Serializable]
    public class KeywordEntry
    {
        public List<string> keywords = new List<string>();
    }
    
    public int callbackOrder { get { return -5; } }
    
    // the Shader variant stripping callback called when building the Player or AssetBundles
    // https://blogs.unity3d.com/2018/05/14/stripping-scriptable-shader-variants/
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> shaderCompilerData)
    {
        if (enabled)
        {
            // TODO: allow for several of these to exist alongside one another
            CustomShaderVariantCollection customShaderVariantCollection = (CustomShaderVariantCollection)AssetDatabase.LoadAssetAtPath("Assets/Editor/CustomShaderVariantCollection.asset", typeof(CustomShaderVariantCollection));
        
            // if the shader coming from the callback is in our list
            if (customShaderVariantCollection.shaders.Contains(shader))
            {
                // get the info about that shader
                ShaderEntry shaderEntry = customShaderVariantCollection.shaderEntries[customShaderVariantCollection.shaders.IndexOf(shader)];
                
                // if a shader pass is unnamed
                if (shaderEntry.passNames.Contains("<unnamed>"))
                {
                    // don't strip, we don't know how to handle this case
                    // Debug.Log(shader);
                }
                // if we have the pass in our entry, look to keywords to strip
                else if (shaderEntry.passNames.Contains(snippet.passName))
                {
                    // get the keywordset for a certain pass
                    KeywordSet keywordSet = shaderEntry.keywordSets[shaderEntry.passNames.IndexOf(snippet.passName)];
        
                    // prepare a valid list that we'll use at the end of the keyword checking process
                    List<ShaderCompilerData> validShaderCompilerData = new List<ShaderCompilerData>();
                        
                    // for each ShaderCompilerData entry
                    foreach (ShaderCompilerData shaderCompilerDataEntry in shaderCompilerData)
                    {
                        // prepare a list of keywords from the processor
                        List<ShaderKeyword> shaderKeywordsInEntry = new List<ShaderKeyword>(shaderCompilerDataEntry.shaderKeywordSet.GetShaderKeywords());
                        List<string> stringKeywordsInEntry = new List<string>();
                        
                        // get a list of keywords from the processor
                        foreach (ShaderKeyword shaderKeyword in shaderKeywordsInEntry)
                        {
                            stringKeywordsInEntry.Add(shaderKeyword.GetKeywordName());
                        }
                        
                        // make sure we have an equivalent entry in keywords sets
                        foreach (var keywordEntry in keywordSet.keywordEntries)
                        {
                            // no keywords case
                            if (stringKeywordsInEntry.Count == 0 && keywordEntry.keywords.Contains("<no keywords>"))
                            {
                                validShaderCompilerData.Add(shaderCompilerDataEntry);
                                break;
                            }
                            // keywords case
                            else
                            {
                                // prepare a counter
                                int foundCount = 0;
                                
                                // for each keyword in our info
                                foreach (var keyword in keywordEntry.keywords)
                                {
                                    if (stringKeywordsInEntry.Contains(keyword))
                                    {
                                        foundCount++;
                                    }
                                }
        
                                if (stringKeywordsInEntry.Count == foundCount)
                                {
                                    validShaderCompilerData.Add(shaderCompilerDataEntry);
                                    break;
                                }
                            }
                        }
                    }
        
                    // Debug.Log(shaderCompilerData.Count + " total - " + validShaderCompilerData.Count + " added");

                    // just assigning validShaderCompilerData to shaderCompilerData does not work, more than likely
                    // because of it being an out parameter or the way it's marshalled
                    shaderCompilerData.Clear();
                    shaderCompilerData.AddRange(validShaderCompilerData);
                }
                // if we don't have the pass in our info
                else
                {
                    // strip
                    shaderCompilerData.Clear();
                }
            }
            // if we don't have the shader in our info
            else
            {
                // strip
                shaderCompilerData.Clear();
            }   
        }
    }

    [ContextMenu("Clear")]
    private void Clear()
    {
        shaders.Clear();
        shaderEntries.Clear();
    }

    [ContextMenu("Populate from TextAsset")]
    private void PopulateFromTextAsset()
    {
        var entries = text.ToString().Split('\n');
        foreach (string entry in entries)
        {
            var sections = entry.Split(new string[] { ", " }, StringSplitOptions.None);

            string shaderName;
            Shader shader = null;
            string passName = null;
            string stageName = null;
            List<string> keywords = null;
            ShaderEntry shaderEntry = null;
            KeywordSet keywordSet = null;
            KeywordEntry keywordEntry = null;
            
            foreach (string section in sections)
            {
                var info = section.Split(new string[] { ": " }, StringSplitOptions.None);
                if (info.Length == 2)
                {
                    if(info[0] == "Compiled shader")
                    {
                        shaderName = info[1];
                        shader = Shader.Find(shaderName);
                    }
                    else if(info[0] == "pass")
                    {
                        passName = info[1];
                    }
                    else if (info[0] == "stage")
                    {
                        stageName = info[1];
                    }
                }
                else
                {
                    if (info[0].Contains("<no keywords>"))
                    {
                        keywords = new List<string>();
                        keywords.Add("<no keywords>");
                    }
                    else
                    {
                        keywords = new List<string>(info[0].Split(' '));
                        keywords.RemoveAt(0);
                    }
                }
            }

            if (stageName == "fragment")
            {
                if (shaders.Contains(shader))
                {
                    shaderEntry = shaderEntries[shaders.IndexOf(shader)];
                }
                else
                {
                    shaderEntry = new ShaderEntry();
                    shaders.Add(shader);
                    shaderEntries.Add(shaderEntry);
                }
            
                if (shaderEntry.passNames.Contains(passName))
                {
                    keywordSet = shaderEntry.keywordSets[shaderEntry.passNames.IndexOf(passName)];
                }
                else
                {
                    keywordSet = new KeywordSet();
                    shaderEntry.passNames.Add(passName);
                    shaderEntry.keywordSets.Add(keywordSet);
                }
            
                keywordEntry = new KeywordEntry();
                keywordEntry.keywords = keywords;
            
                keywordSet.keywordEntries.Add(keywordEntry);
            }
        }
    }
}
