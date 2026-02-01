namespace TheGame.Core.OS;

public class SystemAPI : BaseAPI {
    public FileSystemAPI FileSystemAPI { get; private set; }
    public RegistryAPI RegistryAPI { get; private set; }
    public NotificationsAPI NotificationsAPI { get; private set; }
    public HotkeysAPI HotkeysAPI { get; private set; }
    public ClipboardAPI ClipboardAPI { get; private set; }
    public MediaAPI MediaAPI { get; private set; }

    public SystemAPI(Process process) : base(process) {
        FileSystemAPI = new FileSystemAPI(OwningProcess);
        RegistryAPI = new RegistryAPI(OwningProcess);
        NotificationsAPI = new NotificationsAPI(OwningProcess);
        HotkeysAPI = new HotkeysAPI(OwningProcess);
        ClipboardAPI = new ClipboardAPI(OwningProcess);
        MediaAPI = new MediaAPI(OwningProcess);
    }
}
