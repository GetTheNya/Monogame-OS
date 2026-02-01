namespace TheGame.Core.OS;

public abstract class BaseAPI {
    protected readonly Process OwningProcess;

    protected BaseAPI(Process process) {
        OwningProcess = process;
    }
}
