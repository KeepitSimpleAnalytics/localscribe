import language_tool_python
tool = language_tool_python.LanguageTool('en-US')
matches = tool.check('I has a apple')
if matches:
    m = matches[0]
    print(dir(m))
    print(f"Message: {m.message}")
    print(f"Offset: {m.offset}")
    # print(f"Length: {m.errorLength}") # Potentially crashing
