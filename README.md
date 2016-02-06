# ForceDel

ForceDel uses some exciting new APIs introduced with Microsoft Windows Vista to determine which processes have a specified file in use. With this information, ForceDel is able to close the file in the remote process address space and then delete the file.

# Compatibility
Starting with version 1.2, ForceDel can also delete files on Windows XP.

# Limitations
ForceDel is not able to delete any file that is in use. The success of ForceDel depends a little bit on the application that has the file opened and how the file was opened. But in many (maybe most) cases ForceDel is able to delete the file.

# Development
ForceDel is currently not under active development. Do not expect further releases or new features. But pull requests from the community are welcome.
