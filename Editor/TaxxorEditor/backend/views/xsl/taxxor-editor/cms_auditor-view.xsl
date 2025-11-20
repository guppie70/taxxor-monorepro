<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:include href="_utils.xsl"/>
    <xsl:param name="permissions"/>
    <xsl:param name="language1"/>
    <xsl:param name="language2"/>
    <xsl:param name="language3"/>
    <xsl:param name="language4"/>
    <xsl:param name="language5"/>
    <xsl:param name="language6"/>
    <xsl:param name="language7"/>
    <xsl:param name="language8"/>
    <xsl:param name="language9"/>
    
    
    <xsl:output method="html" omit-xml-declaration="yes" indent="yes"/>
    
    <xsl:template match="/">
        <!--<xsl:comment>
            - permissions: <xsl:value-of select="$permissions"/>
        </xsl:comment>-->
        <xsl:apply-templates select="/commits//commit"/>
        <div id="popup-details-content">
            <xsl:if test="contains($permissions, 'restoresection') or contains($permissions, 'restoreversion')">
                <ul class="popup-options">
                    <li class="compare">
                        <div class="mimic-link" onclick="openCompareModalDialog()">Compare with previous version</div>
                    </li>
                    <xsl:if test="contains($permissions, 'restoresection')">
                        <li class="restore">
                            <div class="mimic-link" onclick="openRestoreModalDialog()">Restore this version</div>
                        </li>
                    </xsl:if>
                </ul>
            </xsl:if>
        </div>
    </xsl:template>
    
    <xsl:template match="day">
        <!-- per day -->
        <div class="timeline-container timeline-style2">
            <span class="timeline-label">
                <b>
                    <xsl:value-of select="@label"/>
                </b>
            </span>
            <div class="timeline-items">
                <xsl:apply-templates select="commit"/>
            </div>
        </div>
    </xsl:template>
    
    <xsl:template match="commit">
        <xsl:variable name="sitestructure-id">
            <xsl:call-template name="string-replace-all">
                <xsl:with-param name="text" select="message/id"/>
                <xsl:with-param name="replace">'</xsl:with-param>
                <xsl:with-param name="by">\'</xsl:with-param>
            </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="linkname">
            <xsl:call-template name="string-replace-all">
                <xsl:with-param name="text" select="message/linkname"/>
                <xsl:with-param name="replace">'</xsl:with-param>
                <xsl:with-param name="by">\'</xsl:with-param>
            </xsl:call-template>
        </xsl:variable>
        <xsl:variable name="object-type">
            <xsl:choose>
                <xsl:when test="message/crud and message/crud/@application = 'externaltablesync'">external table</xsl:when>
                <xsl:when test="message/linkname and contains(message/linkname, 'hierarchy')">hierarchy</xsl:when>
                <xsl:otherwise>section</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>       
        
        <div class="timeline-item clearfix">
            <div class="timeline-info">
                <span class="timeline-date" data-epoch="{date/@epoch}">
                    <xsl:value-of select="date/@time"/>
                </span>
                <i class="timeline-indicator btn btn-info no-hover"/>
            </div>
            <div class="widget-box transparent">
                <div class="widget-body">
                    <div class="widget-main no-padding">
                        <span class="bigger-110">
                            <span class="bolder blue">
                                <xsl:value-of select="author/name"/>
                            </span>
                            <xsl:choose>
                                <xsl:when test="message/crud and message/linkname">

                                    <!-- XML Data from the Taxxor Editor was saved -->
                                    <xsl:choose>
                                        <xsl:when test="message/crud = 'c'">
                                            <xsl:value-of select="concat(' created ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'u'">
                                            <xsl:value-of select="concat(' updated ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 's'">
                                            <xsl:value-of select="concat(' synced ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'd'">
                                            <xsl:value-of select="concat(' deleted ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'bulktransform'">
                                            <xsl:value-of select="concat(' transformed multiple ', $object-type, 's ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'transform'">
                                            <xsl:value-of select="concat(' transformed ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'metadataupdatereportingrequirements'">
                                            <xsl:value-of select="concat(' updated metadata ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'clonelanguage'">
                                            <xsl:value-of select="concat(' cloned language ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'findreplace'">
                                            <xsl:value-of select="concat(' replaced content in ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'contentdatarestore'">
                                            <xsl:value-of select="concat(' restored the content in ', $object-type, ' ')"/>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'importstructureddata'">
                                            <xsl:text> imported ERP structured data for this project</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'commitforversion'">
                                            <xsl:text> created a snapshot of all project content using the version manager </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'structureddatasync'">
                                            <xsl:text> updated structured data elements </xsl:text>
                                            <xsl:choose>
                                                <xsl:when test="message/crud/@type='full'">for the complete project</xsl:when>
                                                <xsl:when test="message/crud/@type='section'">in <xsl:value-of select="message/crud/@filecount"/> sections</xsl:when>
                                                <xsl:when test="not(message/linkname/text()='none')">in </xsl:when>
                                                <xsl:otherwise>in section with id <xsl:text disable-output-escaping="yes">&amp;#34;</xsl:text><xsl:value-of select="message/id"/><xsl:text disable-output-escaping="yes">&amp;#34;</xsl:text></xsl:otherwise>
                                            </xsl:choose>
                                        </xsl:when>
                                        <xsl:when test="message/crud = 'sectioncontentclone'">
                                            <xsl:text> cloned section(s) </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'upload'">
                                            <xsl:text> uploaded file</xsl:text>
                                            <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if>
                                            <xsl:text> </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'delete'">
                                            <xsl:choose>
                                                <xsl:when test="contains(message/id, '.xml')"> removed link or visual reference</xsl:when>
                                                <xsl:otherwise> deleted file</xsl:otherwise>
                                            </xsl:choose>
                                            <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if> 
                                            <xsl:if test="not(contains(message/id, '.xml'))">
                                               <xsl:text> or folder</xsl:text>
                                               <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if> 
                                            </xsl:if>
                                            <xsl:text> </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'copy'">
                                            <xsl:text> copied file</xsl:text>
                                            <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if>
                                            <xsl:text> </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'move'">
                                            <xsl:choose>
                                                <xsl:when test="contains(message/id, '.xml')"> updated link or visual reference</xsl:when>
                                                <xsl:otherwise> moved file</xsl:otherwise>
                                            </xsl:choose>                                            
                                            <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if>
                                            <xsl:text> </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'create'">
                                            <xsl:text> created folder </xsl:text>
                                        </xsl:when>
                                        <xsl:when test="message/crud/@application = 'filemanager' and message/crud = 'rename'">
                                            <xsl:choose>
                                                <xsl:when test="contains(message/id, '.xml')"> updated link or visual reference</xsl:when>
                                                <xsl:otherwise> renamed file</xsl:otherwise>
                                            </xsl:choose>
                                            <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if>
                                            <xsl:if test="not(contains(message/id, '.xml'))">
                                                <xsl:text> or folder</xsl:text>
                                                <xsl:if test="contains(message/linkname, ',')"><xsl:text>s</xsl:text></xsl:if>
                                            </xsl:if>
                                            <xsl:text> </xsl:text>
                                        </xsl:when>
                                        
                                        <xsl:otherwise>
                                            <xsl:value-of select="concat(' unknown action to ', $object-type, ' ')"/>
                                        </xsl:otherwise>
                                    </xsl:choose>
                                    <xsl:if test="string-length(normalize-space(message/linkname))>0 and not(message/linkname = 'none')">
                                        <xsl:text disable-output-escaping="yes">&amp;#34;</xsl:text>
                                        <xsl:value-of select="normalize-space(message/linkname)"/>
                                        <xsl:text disable-output-escaping="yes">&amp;#34;</xsl:text>                                        
                                    </xsl:if>
                           
                                    <xsl:choose>
                                        <xsl:when test="message/crud = 'contentdatarestore'">
                                            <xsl:variable name="original-commit-hash" select="message/crud/@originalcommithash"/>
                                            <xsl:if test="string-length($original-commit-hash)>0">
                                                <xsl:text> from timestamp </xsl:text>
                                                <xsl:value-of select="//commit[@hash=$original-commit-hash]/date"/>
                                            </xsl:if>
                                        </xsl:when>
                                    </xsl:choose>
                                    
                                    <!-- Render additional information for admins -->
                                    <xsl:if test="contains($permissions, 'viewdevelopertools')">
                                        <xsl:choose>
                                            <xsl:when test="message/crud = 'c' or message/crud = 'u' or message/crud = 'd'">
                                                <small>
                                                    <xsl:text>(id: </xsl:text>
                                                    <xsl:value-of select="message/id"/>
                                                    <xsl:if test="$object-type = 'section' and message/linkname/@lang and string-length($language2) > 0">
                                                        <xsl:value-of select="concat(', lang: ', message/linkname/@lang)"/>
                                                    </xsl:if>
                                                    <xsl:text>)</xsl:text>
                                                </small>
                                            </xsl:when>
                                        </xsl:choose>
                                    </xsl:if>
                                </xsl:when>
                                <xsl:otherwise>
                                    <xsl:text> </xsl:text>
                                    <xsl:value-of select="message"/>
                                </xsl:otherwise>
                            </xsl:choose>
                        </span>
                        <!-- Render an options button in case the auditor item is referring to a section -->
                        <xsl:if test="message/crud and message/linkname">
                            <xsl:choose>
                                <xsl:when test="message/crud/@application = 'externaltablesync'">
                                    <!-- Do not render anything -->
                                </xsl:when>
                                <xsl:when test="message/id = 'website' and not(contains($permissions, 'restoresection'))">
                                    <!-- Do not render anything because comparison of website content is not possible -->
                                </xsl:when>
                                <xsl:when test="message/id = 'website' and @latest = 'true'">
                                    <!-- In this specific case we do not need to render any options because there is no PDF generation and no restore possible -->
                                </xsl:when>
                                <xsl:when test="message/crud = 'bulktransform' or message/crud = 'commitforversion' or message/crud = 'importstructureddata'">
                                    <!-- Do not render anything for a bulk transform -->
                                </xsl:when>
                                <xsl:otherwise>
                                    <span>
                                        <a tabindex="0" class="btn btn-xs btn-options" data-author="{author/name}" data-dateepoch="{date/@epoch}" data-repro="{@repro}" data-commithash="{@hash}" data-crud="{message/crud}" data-sitestructureid="{$sitestructure-id}" data-linkname="{$linkname}" data-objecttype="{$object-type}">
                                            <xsl:attribute name="data-showrestore">
                                                <xsl:choose>
                                                    <xsl:when test="not(contains($permissions, 'restoresection'))">false</xsl:when>
                                                    <xsl:when test="@latest='true'">false</xsl:when>
                                                    <xsl:otherwise>true</xsl:otherwise>
                                                </xsl:choose>
                                            </xsl:attribute>
                                            <xsl:text>...</xsl:text>
                                        </a>
                                    </span>
                                </xsl:otherwise>
                            </xsl:choose>
                        </xsl:if>
                    </div>
                </div>
            </div>
        </div>
    </xsl:template>
</xsl:stylesheet>
