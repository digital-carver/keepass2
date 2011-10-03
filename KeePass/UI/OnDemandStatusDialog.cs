﻿/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2009 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using KeePass.Forms;

using KeePassLib.Interfaces;

namespace KeePass.UI
{
	public sealed class OnDemandStatusDialog : IStatusLogger
	{
		private bool m_bUseThread;
		private Thread m_th = null;
		private Form m_fOwner;
		private StatusProgressForm m_dlgModal = null;

		private const uint InitialProgress = 0;
		private const string InitialStatus = null;

		private volatile string m_strTitle = null;
		private volatile bool m_bTerminate = false;
		private volatile uint m_uProgress = InitialProgress;
		private volatile string m_strProgress = InitialStatus;

		public OnDemandStatusDialog(bool bUseThread, Form fOwner)
		{
			m_bUseThread = bUseThread;
			m_fOwner = fOwner;
		}

		public void StartLogging(string strOperation, bool bWriteOperationToLog)
		{
			m_strTitle = strOperation;
		}

		public void EndLogging()
		{
			lock(this) { m_bTerminate = true; }
			m_th = null;

			if(m_dlgModal != null)
			{
				DestroyStatusDialog(m_dlgModal);
				m_dlgModal = null;
			}
		}

		public bool SetProgress(uint uPercent)
		{
			lock(this) { m_uProgress = uPercent; }

			return ((m_dlgModal != null) ? m_dlgModal.SetProgress(uPercent) : true);
		}

		public bool SetText(string strNewText, LogStatusType lsType)
		{
			if(strNewText == null) return true;
			if(lsType != LogStatusType.Info) return true;

			if(m_bUseThread && (m_th == null))
			{
				ThreadStart ts = new ThreadStart(this.GuiThread);
				m_th = new Thread(ts);
				m_th.Start();
			}
			if(!m_bUseThread && (m_dlgModal == null))
				m_dlgModal = ConstructStatusDialog();

			lock(this) { m_strProgress = strNewText; }
			return ((m_dlgModal != null) ? m_dlgModal.SetText(strNewText, lsType) : true);
		}

		public bool ContinueWork()
		{
			return ((m_dlgModal != null) ? m_dlgModal.ContinueWork() : true);
		}

		private void GuiThread()
		{
			uint uProgress = InitialProgress;
			string strProgress = InitialStatus;

			StatusProgressForm dlg = null;
			while(true)
			{
				lock(this)
				{
					if(m_bTerminate) break;

					if(m_uProgress != uProgress)
					{
						uProgress = m_uProgress;
						if(dlg != null) dlg.SetProgress(uProgress);
					}

					if(m_strProgress != strProgress)
					{
						strProgress = m_strProgress;

						if(dlg == null) dlg = ConstructStatusDialog();

						dlg.SetText(strProgress, LogStatusType.Info);
					}
				}

				Application.DoEvents();
			}

			DestroyStatusDialog(dlg);
		}

		private StatusProgressForm ConstructStatusDialog()
		{
			StatusProgressForm dlg = new StatusProgressForm();
			dlg.InitEx(m_strTitle, false, true, m_bUseThread ? null : m_fOwner);
			dlg.Show();
			dlg.StartLogging(null, false);
			dlg.SetProgress(m_uProgress);

			MainForm mfOwner = ((m_fOwner != null) ? (m_fOwner as MainForm) : null);
			if((m_bUseThread == false) && (mfOwner != null))
			{
				mfOwner.RedirectActivationPush(dlg);
				mfOwner.UIBlockInteraction(true);
			}

			return dlg;
		}

		private void DestroyStatusDialog(StatusProgressForm dlg)
		{
			if(dlg != null)
			{
				MainForm mfOwner = ((m_fOwner != null) ? (m_fOwner as MainForm) : null);
				if((m_bUseThread == false) && (mfOwner != null))
				{
					mfOwner.RedirectActivationPop();
					mfOwner.UIBlockInteraction(false);
				}

				dlg.EndLogging();
				dlg.Close();
				dlg.Dispose();

				if(mfOwner != null) mfOwner.Activate(); // Prevent disappearing
			}
		}
	}
}