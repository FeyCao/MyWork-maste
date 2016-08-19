using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;


namespace KGameServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Thread initThread;
        private ServerInst serverInst;

        public MainWindow()
        {
            InitializeComponent();
            serverInst = new ServerInst();

            lblOnlineCount.DataContext = serverInst.PlayerConnectionMap;
            lblOnlineUserCount.DataContext = serverInst.PlayerConnectionMap;
            lblNowMatchCount.DataContext = serverInst.MatchMaker;
            lblWaitngPlayerCount.DataContext = serverInst.MatchMaker;

            initThread = new Thread(InitThreadProc);
            initThread.IsBackground = true;
            initThread.Start();
        }

        private void InitThreadProc()
        {
            DBManager.Init();
            HisDataMng.Init();
            serverInst.Init();

           
        }


       

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            HisDataMng.RequestHistoryData(OnReceiveHistoryDataCallBack);
        }

         private void OnReceiveHistoryDataCallBack(List<DayData> historyDataList,CodeInfo codeInfo)
        {

        }
    }
}
