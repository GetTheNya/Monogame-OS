namespace TheGame.Core.OS;

public abstract class BaseAPI {
    protected readonly Process OwningProcess;

    protected string AppId => OwningProcess.AppId;

    protected BaseAPI(Process process) {
        OwningProcess = process;
    }
}
