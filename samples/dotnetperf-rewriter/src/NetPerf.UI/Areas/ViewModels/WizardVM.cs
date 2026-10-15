using System.ComponentModel;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Infrastructure;
using NetPerf.Common.Validation;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	internal class WizardVM : Conductor<NPScreen>.Collection.OneActive {

		private int m_activeIndex = -1;

		/// <summary>
		/// <see cref="true"/> if each step must be completed before moving to the next step, 
		/// <see cref="false"/> if the steps can be completed in any order
		/// </summary>
		public bool IsSequentialWizard {
			get;
			protected set;
		}

		protected override void OnActivate() {

			for (int i = 0; i < Items.Count; i++) {
				Items[i].PropertyChanged += OnWizardScreen_PropertyChanged;
			}

			NextStep();

			base.OnActivate();
		}

		protected override void OnDeactivate(bool close) {
			base.OnDeactivate(close);

			if (close) {
				for (int i = 0; i < Items.Count; i++) {
					Items[i].PropertyChanged -= OnWizardScreen_PropertyChanged;
				}
			}
		}

		protected virtual void OnWizardScreen_PropertyChanged(object sender, PropertyChangedEventArgs args) {
			NotifyOfPropertyChange(() => CanFinishWizard);
			NotifyOfPropertyChange(() => CanNextStep);
			NotifyOfPropertyChange(() => CanPreviousStep);
		}

		public override void ActivateItem(NPScreen item) {
			base.ActivateItem(item);

			// set the index to the index of the activated item so that
			// the Next and Previous buttons still perform as expected
			m_activeIndex = Items.IndexOf(item);
			NotifyOfPropertyChange(() => CanFinishWizard);
			NotifyOfPropertyChange(() => CanNextStep);
			NotifyOfPropertyChange(() => CanPreviousStep);
		}

		private void ActivateCurrentItem() {
			if (m_activeIndex >= 0 && m_activeIndex < Items.Count) {
				ActivateItem(Items[m_activeIndex]);
			}
		}

		public virtual bool CanPreviousStep {
			get {
				return m_activeIndex > 0;
			}
		}

		public virtual void PreviousStep() {
			m_activeIndex -= 1;
			if (m_activeIndex < 0) {
				m_activeIndex = 0;
			}

			ActivateCurrentItem();
		}

		public virtual bool CanNextStep {
			get {
				ISelfValidate validator = null;
				if (m_activeIndex >= 0 && m_activeIndex < Items.Count) {
					validator = ActiveItem as ISelfValidate;
				}

				return m_activeIndex < Items.Count - 1
					&& (!IsSequentialWizard || validator == null || validator.IsValid);
			}
		}

		public virtual void NextStep() {
			m_activeIndex += 1;
			if (m_activeIndex >= Items.Count) {
				m_activeIndex = Items.Count - 1;
			}

			ActivateCurrentItem();
		}

		public virtual bool CanFinishWizard {
			get {
				return m_activeIndex == Items.Count - 1;
			}
		}

		public virtual void FinishWizard() {
			this.TryClose();
		}

		public virtual void CancelWizard() {
			this.TryClose();
		}


	}
}
