# ForceDel
ForceDel is a useful utility that enables you to delete a file that is currently in use or locked by another application. 

ForceDel works on systems running Windows XP (or newer) and needs the .NET Framework in Version 3.5. ForceDel itself comes as a single execuatble - no external DLLs are required. 

But be warned. There is no official way to delete a file that is in use by another process. ForceDel uses tricks to archive that and some applications may be offended if a file handle becomes unexpectedly invalid

# How does is work
Well, it is quite easy: ForceDel determines witch processes are holding locks on the file and then closes the file so it can be deleted. Easy, eh? 

NO, IT IS NOT! 

The first thing ForceDel has to do is to determine wich process(es) have the file opened or locked. On Windows Vista (and newer) there is a new [Restart Manager API](http://msdn.microsoft.com/en-us/library/windows/desktop/cc948910%28v=vs.85%29.aspx) that can be used to get this information easily. On Windows XP you have to use the badly documented Windows function [NtQuerySystemInformation](http://msdn.microsoft.com/en-us/library/windows/desktop/ms724509%28v=vs.85%29.aspx) to get all currently running processes with their opened files (file handles). 

Now that ForceDel knows the processes and the handles, the next challenge is to get the file name from every file handle. This can be done with the Windows function [NtQueryObject](http://msdn.microsoft.com/en-us/library/bb432383%28v=vs.85%29.aspx). But pay attention - this function will block for various handle types (i. e. for named pipes). To bypass this incredible blocking feature, ForceDel issues this function in a thread - if the thread is not able to get the filename within a specified time, ForceDel skips this handle and continues with the next one. So some file handles are skipped/ignored - but ForceDel will not block forever. 

On Windows Vista and newer there is a function named [GetFileInformationByHandleEx](http://msdn.microsoft.com/en-us/library/windows/desktop/aa364953%28v=vs.85%29.aspx) that can determine the filename for a handle, but tests showed that this function is somehow limited and can not determine the file names in all cases. So even on these systems ForceDel uses the function [NtQueryObject](http://msdn.microsoft.com/en-us/library/bb432383%28v=vs.85%29.aspx). 

When ForceDel got the right handle (a handle for the file to be deleted), ForceDel uses a special feature of the Windows API. The function [DuplicateHandle](http://msdn.microsoft.com/en-us/library/windows/desktop/ms724251%28v=vs.85%29.aspx) is used to make a copy of the file handle - but in a way that the file handle is not just copied, it is also closed in the process that has the file open. Then ForceDel closes the file too and deletes is. Thats it! 

Over 500 lines of code just to delete a file...

# Development
ForceDel is currently not under active development. Do not expect further releases or new features. But pull requests from the community are welcome.

# Licence
ForceDel has been dedicated to the public domain. But if you did some enhancements or bug fixes to ForceDel please provide me with the changes (pull requests) so the community can participate from your changes. This is how the community works...
