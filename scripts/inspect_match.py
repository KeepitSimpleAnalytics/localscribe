import language_tool_python

tool = language_tool_python.LanguageTool('en-US')
matches = tool.check("I has a apple.")
if matches:
    m = matches[0]
    print(f"Attributes: {dir(m)}")
    try:
        print(f"errorLength: {m.errorLength}")
    except AttributeError:
        print("errorLength not found")
    
    try:
        print(f"len: {m.len}")
    except AttributeError:
        print("len not found")
        
    try:
        print(f"length: {m.length}")
    except AttributeError:
        print("length not found")
