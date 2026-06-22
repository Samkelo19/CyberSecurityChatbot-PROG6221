using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using MySql.Data.MySqlClient;

namespace CyberSecurityBotGUI1
{
    
    /// MainWindow - Cybersecurity Awareness Chatbot (Part 3 / POE - Final Submission)
    /// PROG6221 - Fully meets all requirements:
    /// • Task Assistant with MySQL Database (CRUD)
    /// • Cybersecurity Mini-Game (Quiz)
    /// • NLP Simulation via Keyword Detection
    /// • Activity Log Feature
    /// All features integrated into one professional GUI chatbot.
   
    public partial class MainWindow : Window
    {
        // ==================================================
        // MEMORY & USER TRACKING (from Part 2)
        // ==================================================
        private string favouriteTopic = "";
        private readonly Random random = new Random();

        // ==================================================
        // TASK ASSISTANT - MySQL Persistent (Task 1)
        // ==================================================
        private List<TaskItem> taskList = new List<TaskItem>();
        private bool waitingForReminder = false;
        private string pendingTaskTitle = "";
        private string userName = ""; //Will remember the user's name

        // Needed assistance for MySQL and had to use chat
        private const string ConnectionString = "Server=localhost;Database=cyberbotdb;Uid=root;Pwd=@Labs2026!;";

        private class TaskItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Reminder { get; set; }
            public bool IsCompleted { get; set; } = false;
        }

        // ==================================================
        // QUIZ SYSTEM (Task 2)
        // ==================================================
        private bool quizActive = false;
        private int quizScore = 0;
        private int currentQuestionIndex = 0;

        private string[,] quizQuestions = new string[,]
        {
            { "What should you do if you receive an email asking for your password?", "A) Reply with your password", "B) Delete the email", "C) Report the email as phishing", "D) Ignore it", "C", "Correct! Reporting phishing emails helps prevent scams." },
            { "Which of the following is the STRONGEST password?", "A) password123", "B) MyDog2015", "C) qwerty", "D) X$7mK!9pL@2z", "D", "Correct! Use long complex passwords with symbols." },
            { "True or False: Using the same password for all accounts is safe.", "A) True", "B) False", "C) Only if strong", "D) Only for email", "B", "Correct! One breach compromises everything." },
            { "What is malware?", "A) Hardware", "B) Malicious software", "C) Firewall", "D) VPN", "B", "Correct! Malware = malicious software." },
            { "Which is a sign of phishing?", "A) From a friend", "B) Urgent language + link", "C) Contains your name", "D) No attachments", "B", "Correct! Urgency is a red flag." },
            { "What does VPN stand for?", "A) Virtual Private Network", "B) Very Protected Node", "C) Virus Protection Network", "D) Virtual Password Node", "A", "Correct! VPN encrypts your connection." },
            { "True or False: Public Wi-Fi is safe for banking.", "A) True", "B) False", "C) Only on weekends", "D) With antivirus", "B", "Correct! Public Wi-Fi is usually unencrypted." },
            { "What is two-factor authentication (2FA)?", "A) Two passwords", "B) Extra verification step", "C) Logging in twice", "D) Antivirus feature", "B", "Correct! 2FA greatly improves security." },
            { "What is ransomware?", "A) Speeds up PC", "B) Locks files and demands payment", "C) Antivirus", "D) Secure browser", "B", "Correct! Ransomware encrypts your data." },
            { "Best protection against malware?", "A) Open all attachments", "B) Download from anywhere", "C) Keep software updated", "D) Share passwords", "C", "Correct! Updates patch security holes." },
            { "What is social engineering?", "A) Building apps", "B) Manipulating people", "C) Improving Wi-Fi", "D) Creating passwords", "B", "Correct! It targets humans." },
            { "True or False: Antivirus protects against ALL threats.", "A) True", "B) False", "C) Only Windows", "D) With updates", "B", "Correct! Defense in depth is needed." }
        };

        // ==================================================
        // ACTIVITY LOG (Task 4)
        // ==================================================
        private List<string> activityLog = new List<string>();
        private const int MAX_LOG_DISPLAY = 10;

        // ==================================================
        // CONSTRUCTOR
        // ==================================================
        public MainWindow()
        {
            InitializeComponent();
            LoadTasksFromDatabase();

            BotReply("Hey there! 👋 Before we start, what's your name?");
            // The bot will wait for the name in the input handler
            LogActivity("Chatbot started successfully.");
        }

        // ==================================================
        // DATABASE METHODS
        // ==================================================
        private void LoadTasksFromDatabase()
        {
            taskList.Clear();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM tasks ORDER BY Id DESC";
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            taskList.Add(new TaskItem
                            {
                                Id = reader.GetInt32("Id"),
                                Title = reader.GetString("Title"),
                                Description = reader.GetString("Description"),
                                Reminder = reader.IsDBNull(reader.GetOrdinal("Reminder")) ? null : reader.GetString("Reminder"),
                                IsCompleted = reader.GetBoolean("IsCompleted")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BotReply($"⚠️ Database error: {ex.Message}");
                MessageBox.Show(ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveTaskToDatabase(TaskItem task)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "INSERT INTO tasks (Title, Description, Reminder, IsCompleted) VALUES (@t, @d, @r, @c)";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@t", task.Title);
                    cmd.Parameters.AddWithValue("@d", task.Description);
                    cmd.Parameters.AddWithValue("@r", (object)task.Reminder ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@c", task.IsCompleted);
                    cmd.ExecuteNonQuery();
                    task.Id = (int)cmd.LastInsertedId;
                }
            }
        }

        private void UpdateTaskInDatabase(TaskItem task)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE tasks SET IsCompleted=@c, Reminder=@r WHERE Id=@id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@c", task.IsCompleted);
                    cmd.Parameters.AddWithValue("@r", (object)task.Reminder ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", task.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteTaskFromDatabase(int id)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "DELETE FROM tasks WHERE Id=@id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ==================================================
        // INPUT HANDLING
        // ==================================================
        private void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendButton_Click(sender, null);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string input = UserInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            ChatArea.Text += $"\n[{DateTime.Now:HH:mm:ss}] YOU > {input}\n\n";
            string lower = input.ToLower();
            UserInput.Clear();

            if (quizActive)
            {
                HandleQuizAnswer(lower);
                return;
            }

            if (waitingForReminder)
            {
                HandleReminderResponse(input);
                return;
            }

            // === NAME HANDLING ===
            if (string.IsNullOrEmpty(userName))
            {
                userName = input;  // Take first input as name
                BotReply($"Nice to meet you, **{userName}**! 😊 I'm your Cybersecurity Buddy.");
                BotReply("How can I help you stay safe online today?");
                LogActivity($"User identified as: {userName}");
                return;
            }

            // === ADDED MORE CONVERSATIONS ===
            if (ContainsAny(lower, new[] { "hi", "hello", "hey", "sup" }))
            {
                BotReply($"Hey {userName}! 👋 Great to see you again!");
            }
            else if (ContainsAny(lower, new[] { "how are you", "how r u" }))
            {
                BotReply($"I'm doing great, {userName}! Thanks for asking. 🔥 How about you?");
            }
            else if (ContainsAny(lower, new[] { "joke", "funny" }))
            {
                BotReply($"Here's one for you, {userName}: Why do cybersecurity experts never get lost?\nBecause they always follow the secure path! 😂");
            }
            else if (ContainsAny(lower, new[] { "thank you", "thanks", "thx" }))
            {
                BotReply($"You're welcome, {userName}! I'm happy to help.");
            }
            else if (ContainsAny(lower, new[] { "bye", "goodbye", "see you" }))
            {
                BotReply($"Bye for now, {userName}! Stay safe and come back anytime! 👋");
                LogActivity("User ended the chat session.");
                return;
            }
            // Main Commands
            else if (ContainsAny(lower, new[] { "add task", "new task", "create task", "remind me to" }))
                HandleAddTask(input);
            else if (ContainsAny(lower, new[] { "view tasks", "show tasks", "list tasks", "my tasks" }))
                ShowTasks();
            else if (ContainsAny(lower, new[] { "mark complete", "complete task", "done" }))
                HandleMarkComplete(input);
            else if (ContainsAny(lower, new[] { "delete task", "remove task" }))
                HandleDeleteTask(input);
            else if (ContainsAny(lower, new[] { "activity log", "show log", "what have you done", "history" }))
                ShowActivityLog();
            else if (ContainsAny(lower, new[] { "quiz", "start quiz", "test me", "game" }))
                StartQuiz();
            else
            {
                // Fun responses for random/off-topic messages
                if (ContainsAny(lower, new[] { "football", "soccer", "sports" }))
                {
                    BotReply($"Haha, {userName}! ⚽ While I love a good football match, my real superpower is cybersecurity. " +
                              "Just like defenders protect the goal, we need to protect our data! Want a security tip or shall we add a task?");
                }
                else if (ContainsAny(lower, new[] { "weather", "rain", "sunny" }))
                {
                    BotReply($"The weather might be unpredictable, but cyber threats are certain! ☀️\n\n" +
                              $"Stay safe online, {userName}. Anything cybersecurity-related I can help with today?");
                }
                else if (ContainsAny(lower, new[] { "music", "song", "sing" }))
                {
                    BotReply($"🎵 Why did the computer go to music school? To improve its 'algorithm'! 😂\n\n" +
                              $"Back to business, {userName} — shall we add a task or start the quiz?");
                }
                else if (ContainsAny(lower, new[] { "food", "hungry", "pizza" }))
                {
                    BotReply($"Mmm, pizza sounds good! 🍕 But never share your passwords as easily as you share pizza slices! 😂\n\n" +
                              $"What cybersecurity task can I help you with today, {userName}?");
                }
                else
                {
                    BotReply($"Hmm, interesting topic, {userName} 😄 I'm still learning a lot, but my main focus is keeping you safe online.\n\n" +
                             "I'm really good at:\n" +
                             "• Adding tasks & reminders\n" +
                             "• Running a fun cybersecurity quiz\n" +
                             "• Showing activity logs\n\n" +
                             "What would you like to do?");
                }
            }
        }

        private bool ContainsAny(string text, string[] keywords) => keywords.Any(k => text.Contains(k));

        // ==================================================
        // TASK ASSISTANT METHODS
        // ==================================================
        private void HandleAddTask(string input)
        {
            string taskName = input;
            string[] remove = { "add task", "new task", "create task", "remind me to" };
            foreach (var w in remove) taskName = taskName.Replace(w, "").Trim();

            if (string.IsNullOrWhiteSpace(taskName))
            {
                BotReply("What task would you like to add?");
                return;
            }

            taskName = char.ToUpper(taskName[0]) + taskName.Substring(1);
            var newTask = new TaskItem { Title = taskName, Description = "Cybersecurity task" };

            SaveTaskToDatabase(newTask);
            taskList.Insert(0, newTask);

            BotReply($"✅ Task added: '{taskName}'");
            BotReply("Would you like to set a reminder? (yes/no)");
            waitingForReminder = true;
            pendingTaskTitle = taskName;
            LogActivity($"Task added: {taskName}");
        }

        private void HandleReminderResponse(string input)
        {
            waitingForReminder = false;
            if (ContainsAny(input.ToLower(), new[] { "yes", "yeah", "sure" }))
            {
                BotReply("How many days from now? (Enter a number)");
                waitingForReminder = true;
                pendingTaskTitle = "REMINDER:" + pendingTaskTitle;
                return;
            }

            if (pendingTaskTitle.StartsWith("REMINDER:"))
            {
                string title = pendingTaskTitle.Replace("REMINDER:", "");
                if (int.TryParse(input.Trim(), out int days) && days > 0)
                {
                    var task = taskList.FirstOrDefault(t => t.Title == title);
                    if (task != null)
                    {
                        task.Reminder = $"In {days} day(s)";
                        UpdateTaskInDatabase(task);
                        BotReply($"⏰ Reminder set for '{title}' in {days} day(s).");
                        LogActivity($"Reminder set for: {title}");
                    }
                }
            }
            pendingTaskTitle = "";
        }

        private void HandleMarkComplete(string input)
        {
            // Simple number extraction
            if (int.TryParse(new string(input.Where(char.IsDigit).ToArray()), out int num) && num > 0)
            {
                LoadTasksFromDatabase();
                if (num <= taskList.Count)
                {
                    var task = taskList[num - 1];
                    task.IsCompleted = true;
                    UpdateTaskInDatabase(task);
                    BotReply($"✅ Task {num} marked as complete!");
                    LogActivity($"Task marked complete: {task.Title}");
                }
            }
            else BotReply("Please specify task number, e.g., 'mark complete 1'");
        }

        private void HandleDeleteTask(string input)
        {
            if (int.TryParse(new string(input.Where(char.IsDigit).ToArray()), out int num) && num > 0)
            {
                LoadTasksFromDatabase();
                if (num <= taskList.Count)
                {
                    var task = taskList[num - 1];
                    DeleteTaskFromDatabase(task.Id);
                    BotReply($"🗑️ Task {num} deleted.");
                    LogActivity($"Task deleted: {task.Title}");
                    LoadTasksFromDatabase();
                }
            }
            else BotReply("Use 'delete task 1' format.");
        }

        private void ShowTasks()
        {
            LoadTasksFromDatabase();
            if (taskList.Count == 0)
            {
                BotReply("No tasks yet. Add one with 'add task'.");
                return;
            }

            BotReply($"📋 Your Tasks ({taskList.Count}):");
            for (int i = 0; i < taskList.Count; i++)
            {
                var t = taskList[i];
                string status = t.IsCompleted ? "✅" : "⬜";
                string rem = string.IsNullOrEmpty(t.Reminder) ? "" : $" ⏰ {t.Reminder}";
                ChatArea.Text += $"   {i + 1}. {status} {t.Title}{rem}\n";
            }
        }

        // ==================================================
        // QUIZ, ACTIVITY LOG, HELPERS
        // ==================================================
        private void StartQuiz()
        {
            quizActive = true;
            quizScore = 0;
            currentQuestionIndex = 0;
            BotReply("🎮 Cybersecurity Quiz Started! Answer with A, B, C, or D.");
            LogActivity("Quiz started");
            DisplayCurrentQuestion();
        }

        private void DisplayCurrentQuestion()
        {
            if (currentQuestionIndex >= quizQuestions.GetLength(0))
            {
                EndQuiz();
                return;
            }
            int q = currentQuestionIndex;
            ChatArea.Text += $"\n❓ Question {q + 1}: {quizQuestions[q, 0]}\n\n";
            for (int i = 1; i <= 4; i++) ChatArea.Text += $"   {quizQuestions[q, i]}\n";
        }

        private void HandleQuizAnswer(string answer)
        {
            string correct = quizQuestions[currentQuestionIndex, 5];
            string explanation = quizQuestions[currentQuestionIndex, 6];

            if (answer.ToUpper() == correct)
            {
                quizScore++;
                BotReply($"✅ {explanation}");
            }
            else
            {
                BotReply($"❌ Correct answer was {correct}. {explanation}");
            }

            currentQuestionIndex++;
            if (currentQuestionIndex < quizQuestions.GetLength(0))
                DisplayCurrentQuestion();
            else
                EndQuiz();
        }

        private void EndQuiz()
        {
            quizActive = false;
            BotReply($"🏁 Quiz Complete! Score: {quizScore}/{quizQuestions.GetLength(0)}");
            LogActivity($"Quiz completed - Score: {quizScore}");
        }

        private void ShowActivityLog()
        {
            if (activityLog.Count == 0)
            {
                BotReply("No activity yet.");
                return;
            }
            BotReply("📜 Recent Activity:");
            int start = Math.Max(0, activityLog.Count - MAX_LOG_DISPLAY);
            for (int i = start; i < activityLog.Count; i++)
                ChatArea.Text += $"   {activityLog[i]}\n";
        }

        private void LogActivity(string action)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            activityLog.Add($"[{time}] {action}");
        }

        private void BotReply(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            ChatArea.Text += $"[{timestamp}] BOT > {message}\n\n";
        }

        // Button Helpers (connect these in XAML)
        private void ViewTasks_Click(object sender, RoutedEventArgs e) { ShowTasks(); }
        private void StartQuiz_Click(object sender, RoutedEventArgs e) { if (!quizActive) StartQuiz(); }
        private void ActivityLog_Click(object sender, RoutedEventArgs e) { ShowActivityLog(); }
        private void ClearChat_Click(object sender, RoutedEventArgs e) { ChatArea.Clear(); }
    }
}
