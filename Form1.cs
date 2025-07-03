using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace changeWeChat
{
    public partial class Form1 : Form
    {
        // Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public struct RECT { public int Left, Top, Right, Bottom; }

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        IntPtr hWeChat = IntPtr.Zero;

        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.White;

            // 确保窗口始终显示
            this.Visible = true;

            // 定时器设置
            timer.Interval = 1000; // 改为1秒检查一次
            timer.Tick += (s, e) => SyncWithWeChat();
            timer.Start();

            // 启动时主动查找微信窗口
            FindWeChatWindow();

            if (hWeChat == IntPtr.Zero)
            {
                MessageBox.Show("未找到微信窗口，请先启动微信PC版！\n\n程序将继续运行，定时查找微信窗口。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // 居中显示遮罩层，方便查看效果
                this.SetBounds((Screen.PrimaryScreen.WorkingArea.Width - 800) / 2,
                              (Screen.PrimaryScreen.WorkingArea.Height - 450) / 2, 800, 450);
            }

            // 确保窗口显示在最前面
            this.BringToFront();
        }

        // 改进的微信窗口查找方法
        void FindWeChatWindow()
        {
            hWeChat = IntPtr.Zero;

            // 尝试多种可能的类名
            string[] possibleClassNames = {
                "WeChatMainWndForPC",
                "WeChat",
                "ChatWnd",
                "WeChatMaster",
                "WeChatWindow"
            };

            foreach (string className in possibleClassNames)
            {
                hWeChat = FindWindow(className, null);
                if (hWeChat != IntPtr.Zero)
                {
                    Console.WriteLine($"找到微信窗口，类名：{className}");
                    break;
                }
            }

            // 如果还没找到，尝试通过进程名查找
            if (hWeChat == IntPtr.Zero)
            {
                hWeChat = FindWeChatByProcess();
            }
        }

        // 通过进程名查找微信窗口
        IntPtr FindWeChatByProcess()
        {
            IntPtr weChatHwnd = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);

                try
                {
                    Process process = Process.GetProcessById((int)processId);
                    if (process.ProcessName.ToLower().Contains("wechat"))
                    {
                        // 获取窗口标题
                        System.Text.StringBuilder windowTitle = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, windowTitle, 256);

                        // 获取窗口类名
                        System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                        GetClassName(hWnd, className, 256);

                        Console.WriteLine($"找到微信相关窗口：{windowTitle} - {className}");

                        // 检查是否是主窗口（通常主窗口标题包含"微信"或者有一定的大小）
                        RECT rect;
                        if (GetWindowRect(hWnd, out rect))
                        {
                            int width = rect.Right - rect.Left;
                            int height = rect.Bottom - rect.Top;

                            if (width > 300 && height > 200) // 主窗口通常比较大
                            {
                                weChatHwnd = hWnd;
                                Console.WriteLine($"选择此窗口作为微信主窗口，大小：{width}x{height}");
                                return false; // 停止枚举
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略无法访问的进程
                }

                return true; // 继续枚举
            }, IntPtr.Zero);

            return weChatHwnd;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_LAYERED = 0x80000;
                const int WS_EX_TOPMOST = 0x8;
                // 暂时不使用WS_EX_TRANSPARENT，先确保窗口能正常显示
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOPMOST;
                return cp;
            }
        }

        void SyncWithWeChat()
        {
            if (hWeChat == IntPtr.Zero)
            {
                FindWeChatWindow();
            }

            if (hWeChat != IntPtr.Zero)
            {
                RECT rect;
                if (GetWindowRect(hWeChat, out rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 0 && height > 0)
                    {
                        this.SetBounds(rect.Left, rect.Top, width, height);
                    }
                }
            }
            else
            {
                // 没找到微信时，遮罩层居中显示
                this.SetBounds((Screen.PrimaryScreen.WorkingArea.Width - 800) / 2,
                              (Screen.PrimaryScreen.WorkingArea.Height - 450) / 2, 800, 450);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var imgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zhaohu.png");
                if (System.IO.File.Exists(imgPath))
                {
                    using (Image img = Image.FromFile(imgPath))
                    {
                        e.Graphics.DrawImage(img, 0, 0, this.Width, this.Height);
                    }
                }
                else
                {
                    // 创建一个简单的招呼界面模拟
                    e.Graphics.Clear(Color.FromArgb(245, 245, 245));

                    // 模拟招呼软件的顶部标题栏
                    using (Brush titleBrush = new SolidBrush(Color.FromArgb(64, 158, 255)))
                    {
                        e.Graphics.FillRectangle(titleBrush, 0, 0, this.Width, 50);
                    }

                    // 绘制招呼标题和图标
                    using (Font titleFont = new Font("微软雅黑", 16, FontStyle.Bold))
                    {
                        e.Graphics.DrawString("招呼", titleFont, Brushes.White, new PointF(15, 15));
                    }

                    // 模拟左侧联系人列表
                    using (Brush sidebarBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
                    {
                        e.Graphics.FillRectangle(sidebarBrush, 0, 50, this.Width / 4, this.Height - 50);
                    }

                    // 模拟聊天内容区域
                    using (Brush chatBrush = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillRectangle(chatBrush, this.Width / 4, 50, this.Width * 3 / 4, this.Height - 50);
                    }

                    // 绘制一些模拟的聊天内容
                    using (Font contentFont = new Font("微软雅黑", 10))
                    {
                        // 左侧联系人
                        e.Graphics.DrawString("工作群聊", contentFont, Brushes.Black, new PointF(10, 70));
                        e.Graphics.DrawString("项目讨论", contentFont, Brushes.Black, new PointF(10, 95));
                        e.Graphics.DrawString("技术交流", contentFont, Brushes.Black, new PointF(10, 120));

                        // 右侧聊天内容
                        int chatX = this.Width / 4 + 20;
                        e.Graphics.DrawString("张三：今天的项目进度如何？", contentFont, Brushes.Black, new PointF(chatX, 70));
                        e.Graphics.DrawString("李四：基本完成了，正在测试", contentFont, Brushes.Black, new PointF(chatX, 95));
                        e.Graphics.DrawString("王五：我这边也差不多了", contentFont, Brushes.Black, new PointF(chatX, 120));
                    }

                    // 添加一个明显的提示
                    using (Font tipFont = new Font("微软雅黑", 14, FontStyle.Bold))
                    {
                        string tip = "遮罩效果已启用 - 这是模拟的招呼界面";
                        SizeF tipSize = e.Graphics.MeasureString(tip, tipFont);
                        e.Graphics.DrawString(tip, tipFont, Brushes.Red,
                            new PointF((this.Width - tipSize.Width) / 2, this.Height - 60));
                    }
                }
            }
            catch (Exception ex)
            {
                e.Graphics.Clear(Color.White);
                e.Graphics.DrawString($"显示异常：{ex.Message}", new Font("微软雅黑", 12), Brushes.Red, new PointF(10, 10));
            }

            // 状态信息
            string statusText = hWeChat == IntPtr.Zero ? "未找到微信窗口" : "已找到微信窗口";
            using (Font statusFont = new Font("微软雅黑", 10))
            {
                e.Graphics.DrawString(statusText, statusFont,
                    hWeChat == IntPtr.Zero ? Brushes.Red : Brushes.Green,
                    new PointF(10, this.Height - 25));
            }
        }

        protected override void WndProc(ref Message m)
        {
            // 移除热键处理，直接调用基类方法
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            base.OnFormClosed(e);
        }

        // 添加右键菜单支持
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("重新查找微信", null, (s, args) => FindWeChatWindow());
                menu.Items.Add("退出程序", null, (s, args) => this.Close());
                menu.Show(this, e.Location);
            }
            base.OnMouseClick(e);
        }
    }
}