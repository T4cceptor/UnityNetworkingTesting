// ----------------------------------------------------------------------------
// <copyright file="DynamicPhotonTransformView.cs" company="Innoative GmbH">
//   PhotonNetwork Framework for Unity - Copyright (C) 2016 Exit Games GmbH
// </copyright>
// <summary>
//   Component to synchronize Transforms via PUN PhotonView. Directly based on
//   the PhotonTransformView
// </summary>
// <author>benedikt.reiser@gmail.com</author>
// ----------------------------------------------------------------------------

using UnityEngine;

namespace Innoactive.Toolkit.Multiuser
{

    /// <summary>
    /// This class helps you to synchronize position, rotation and scale
    /// of a GameObject. It also gives you many different options to make
    /// the synchronized values appear smooth, even when the data is only
    /// send a couple of times per second.
    /// Simply add the component to your GameObject and make sure that
    /// the PhotonTransformView is added to the list of observed components
    ///
    /// ------------------------!!!!!!!!!!!!!!!!---------------------------
    /// This dynamic photon transform view is completely based on the base
    /// Photon Tranform view but can be entirely configured at runtime.
    /// With the base view, it is not possible to configure the settings
    /// for position-, rotation- and scalemodel at runtime. With this dynamic
    /// override, it is possible.
    /// ------------------------!!!!!!!!!!!!!!!!---------------------------
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class IA_PhotonTransformView : MonoBehaviour, IPunObservable
    {
        //Since this component is very complex, we seperated it into multiple objects.
        //The PositionModel, RotationModel and ScaleMode store the data you are able to
        //configure in the inspector while the control objects below are actually moving
        //the object and calculating all the inter- and extrapolation

        PhotonTransformViewPositionModel m_PositionModel = new PhotonTransformViewPositionModel();
        PhotonTransformViewRotationModel m_RotationModel = new PhotonTransformViewRotationModel();
        PhotonTransformViewScaleModel m_ScaleModel = new PhotonTransformViewScaleModel();

        PhotonTransformViewPositionControl m_PositionControl;
        PhotonTransformViewRotationControl m_RotationControl;
        PhotonTransformViewScaleControl m_ScaleControl;

        protected PhotonView photonView;

        bool m_ReceivedNetworkUpdate = false;

        /// <summary>
        /// Flag to skip initial data when Object is instantiated and rely on the first deserialized data instead.
        /// </summary>
        bool m_firstTake = false;

        /// <summary>
        /// public getter for the position transform settings
        /// </summary>
        public PhotonTransformViewPositionModel PositionModel
        {
            get
            {
                return m_PositionModel;
            }
        }

        /// <summary>
        /// public getter for the rotation transform settings
        /// </summary>
        public PhotonTransformViewRotationModel RotationModel
        {
            get
            {
                return m_RotationModel;
            }
        }

        /// <summary>
        /// public getter for the scale transform settings
        /// </summary>
        public PhotonTransformViewScaleModel ScaleModel
        {
            get
            {
                return m_ScaleModel;
            }
        }

        protected virtual void Awake()
        {
            this.photonView = GetComponent<PhotonView>();

            this.m_PositionControl = new PhotonTransformViewPositionControl(this.m_PositionModel);
            this.m_RotationControl = new PhotonTransformViewRotationControl(this.m_RotationModel);
            this.m_ScaleControl = new PhotonTransformViewScaleControl(this.m_ScaleModel);
        }

        protected virtual void OnEnable()
        {
            m_firstTake = true;
        }

        protected virtual void Update()
        {
            if (this.photonView == null || this.photonView.isMine == true || PhotonNetwork.connected == false)
            { 
                return;
            }

            this.UpdatePosition();
            this.UpdateRotation();
            this.UpdateScale();
        }

        void UpdatePosition()
        {
            if (this.m_PositionModel.SynchronizeEnabled == false || this.m_ReceivedNetworkUpdate == false)
            {
                return;
            }

            transform.localPosition = this.m_PositionControl.UpdatePosition(transform.localPosition);
        }

        void UpdateRotation()
        {
            if (this.m_RotationModel.SynchronizeEnabled == false || this.m_ReceivedNetworkUpdate == false)
            {
                return;
            }

            transform.localRotation = this.m_RotationControl.GetRotation(transform.localRotation);
        }

        void UpdateScale()
        {
            if (this.m_ScaleModel.SynchronizeEnabled == false || this.m_ReceivedNetworkUpdate == false)
            {
                return;
            }

            transform.localScale = this.m_ScaleControl.GetScale(transform.localScale);
        }

        /// <summary>
        /// These values are synchronized to the remote objects if the interpolation mode
        /// or the extrapolation mode SynchronizeValues is used. Your movement script should pass on
        /// the current speed (in units/second) and turning speed (in angles/second) so the remote
        /// object can use them to predict the objects movement.
        /// </summary>
        /// <param name="speed">The current movement vector of the object in units/second.</param>
        /// <param name="turnSpeed">The current turn speed of the object in angles/second.</param>
        public virtual void SetSynchronizedValues(Vector3 speed, float turnSpeed)
        {
            this.m_PositionControl.SetSynchronizedValues(speed, turnSpeed);
        }

        public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            this.m_PositionControl.OnPhotonSerializeView(transform.localPosition, stream, info);
            this.m_RotationControl.OnPhotonSerializeView(transform.localRotation, stream, info);
            this.m_ScaleControl.OnPhotonSerializeView(transform.localScale, stream, info);

            if (this.photonView.isMine == false && this.m_PositionModel.DrawErrorGizmo == true)
            {
                this.DoDrawEstimatedPositionError();
            }

            if (stream.isReading == true)
            {
                this.m_ReceivedNetworkUpdate = true;

                // force latest data to avoid initial drifts when player is instantiated.
                if (m_firstTake)
                {
                    m_firstTake = false;

                    if (this.m_PositionModel.SynchronizeEnabled)
                    {
                        this.transform.localPosition = this.m_PositionControl.GetNetworkPosition();
                    }

                    if (this.m_RotationModel.SynchronizeEnabled)
                    {
                        this.transform.localRotation = this.m_RotationControl.GetNetworkRotation();
                    }

                    if (this.m_ScaleModel.SynchronizeEnabled)
                    {
                        this.transform.localScale = this.m_ScaleControl.GetNetworkScale();
                    }

                }

            }
        }

        //void OnDrawGizmos()
        //{
        //    if( Application.isPlaying == false || photonView == null || photonView.isMine == true || PhotonNetwork.connected == false )
        //    {
        //        return;
        //    }

        //    DoDrawNetworkPositionGizmo();
        //    DoDrawExtrapolatedPositionGizmo();
        //}

        void DoDrawEstimatedPositionError()
        {
            Vector3 targetPosition = this.m_PositionControl.GetNetworkPosition();

            // we are synchronizing the localPosition, so we need to add the parent position for a proper positioning.
            if (transform.parent != null)
            {
                targetPosition = transform.parent.position + targetPosition;
            }

            Debug.DrawLine(targetPosition, transform.position, Color.red, 2f);
            Debug.DrawLine(transform.position, transform.position + Vector3.up, Color.green, 2f);
            Debug.DrawLine(targetPosition, targetPosition + Vector3.up, Color.red, 2f);
        }

        //void DoDrawNetworkPositionGizmo()
        //{
        //    if( m_PositionModel.DrawNetworkGizmo == false || m_PositionControl == null )
        //    {
        //        return;
        //    }

        //    ExitGames.Client.GUI.GizmoTypeDrawer.Draw( m_PositionControl.GetNetworkPosition(),
        //                                               m_PositionModel.NetworkGizmoType,
        //                                               m_PositionModel.NetworkGizmoColor,
        //                                               m_PositionModel.NetworkGizmoSize );
        //}

        //void DoDrawExtrapolatedPositionGizmo()
        //{
        //    if( m_PositionModel.DrawExtrapolatedGizmo == false ||
        //        m_PositionModel.ExtrapolateOption == PhotonTransformViewPositionModel.ExtrapolateOptions.Disabled ||
        //        m_PositionControl == null )
        //    {
        //        return;
        //    }

        //    ExitGames.Client.GUI.GizmoTypeDrawer.Draw( m_PositionControl.GetNetworkPosition() + m_PositionControl.GetExtrapolatedPositionOffset(),
        //                                               m_PositionModel.ExtrapolatedGizmoType,
        //                                               m_PositionModel.ExtrapolatedGizmoColor,
        //                                               m_PositionModel.ExtrapolatedGizmoSize );
        //}
    }
}